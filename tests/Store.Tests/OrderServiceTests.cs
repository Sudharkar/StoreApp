using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.DTOs;
using Store.Application.Exceptions;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Data;
using Store.Infrastructure.Services;
using Xunit;

namespace Store.Tests;

public class OrderServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly StoreDbContext _db;
    private readonly OrderService _svc;

    public OrderServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(_conn).Options;
        _db = new StoreDbContext(options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new User { FullName = "Test Customer", Email = "c@test.com", PasswordHash = "x", Role = UserRole.Customer });
        _db.Users.Add(new User { FullName = "Other Customer", Email = "o@test.com", PasswordHash = "x", Role = UserRole.Customer });
        _db.Products.Add(new Product { Sku = "A", Name = "Item A", Price = 100m, StockQuantity = 10, IsActive = true, RowVersion = new byte[] { 1 } });
        _db.Products.Add(new Product { Sku = "B", Name = "Item B", Price = 200m, StockQuantity = 1, IsActive = true, RowVersion = new byte[] { 1 } });
        _db.SaveChanges();

        _svc = new OrderService(_db, new MemoryCache(new MemoryCacheOptions()), NullLogger<OrderService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static CreateOrderRequest Req(params (int productId, int qty)[] items) => new()
    {
        Items = items.Select(i => new OrderItemRequest { ProductId = i.productId, Quantity = i.qty }).ToList()
    };

    private int Stock(int productId) => _db.Products.AsNoTracking().First(p => p.Id == productId).StockQuantity;

    // ---------- Rule 1 ----------
    [Fact]
    public async Task Place_order_reduces_stock_and_calculates_totals()
    {
        var (order, created) = await _svc.PlaceOrderAsync(1, null, Req((1, 3)));

        created.Should().BeTrue();
        order.SubTotal.Should().Be(300m);
        order.Total.Should().Be(300m);
        order.Status.Should().Be("Pending");
        Stock(1).Should().Be(7);
    }

    [Fact]
    public async Task Order_fails_and_no_stock_changes_when_one_item_is_short()
    {
        // item 1 is fine (qty 2), item 2 has only 1 in stock (qty 5) => whole order must fail
        var act = () => _svc.PlaceOrderAsync(1, null, Req((1, 2), (2, 5)));

        await act.Should().ThrowAsync<ConflictException>();
        Stock(1).Should().Be(10); // unchanged
        Stock(2).Should().Be(1);  // unchanged
        _db.Orders.Count().Should().Be(0);
    }

    [Fact]
    public async Task Unknown_product_fails_the_order()
    {
        var act = () => _svc.PlaceOrderAsync(1, null, Req((999, 1)));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- Rule 3 ----------
    [Fact]
    public async Task Same_idempotency_key_returns_original_order_and_reduces_stock_once()
    {
        var (first, created1) = await _svc.PlaceOrderAsync(1, "key-1", Req((1, 2)));
        var (second, created2) = await _svc.PlaceOrderAsync(1, "key-1", Req((1, 2)));

        created1.Should().BeTrue();
        created2.Should().BeFalse();
        second.Id.Should().Be(first.Id);
        _db.Orders.Count().Should().Be(1);
        Stock(1).Should().Be(8); // reduced only once
    }

    [Fact]
    public async Task Different_idempotency_keys_create_different_orders()
    {
        var (a, _) = await _svc.PlaceOrderAsync(1, "key-a", Req((1, 1)));
        var (b, _) = await _svc.PlaceOrderAsync(1, "key-b", Req((1, 1)));

        b.Id.Should().NotBe(a.Id);
        Stock(1).Should().Be(8);
    }

    // ---------- Rule 5 through the service ----------
    [Fact]
    public async Task Fifth_order_of_a_customer_gets_loyalty_discount()
    {
        for (var i = 0; i < 4; i++)
            await _svc.PlaceOrderAsync(1, null, Req((1, 1)));

        var (fifth, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 1)));

        fifth.Discount.Should().Be(10m); // 10% of 100
        fifth.Total.Should().Be(90m);
    }

    // ---------- Rule 4 ----------
    [Fact]
    public async Task Cancel_pending_order_restores_stock()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 4)));
        Stock(1).Should().Be(6);

        var result = await _svc.UpdateStatusAsync(order.Id, "Cancelled", 1, false);

        result.Status.Should().Be("Cancelled");
        Stock(1).Should().Be(10);
    }

    [Fact]
    public async Task Cancelling_twice_is_rejected_and_stock_is_restored_only_once()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 4)));
        await _svc.UpdateStatusAsync(order.Id, "Cancelled", 1, false);

        var act = () => _svc.UpdateStatusAsync(order.Id, "Cancelled", 1, true);

        await act.Should().ThrowAsync<ValidationException>();
        Stock(1).Should().Be(10);
    }

    [Fact]
    public async Task Admin_cannot_jump_from_Pending_to_Shipped()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 1)));

        var act = () => _svc.UpdateStatusAsync(order.Id, "Shipped", 99, true);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Invalid status transition*");
    }

    [Fact]
    public async Task Shipped_order_is_final()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 1)));
        await _svc.UpdateStatusAsync(order.Id, "Confirmed", 99, true);
        await _svc.UpdateStatusAsync(order.Id, "Shipped", 99, true);

        var act = () => _svc.UpdateStatusAsync(order.Id, "Cancelled", 99, true);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Customer_cannot_cancel_someone_elses_order()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 1)));

        var act = () => _svc.UpdateStatusAsync(order.Id, "Cancelled", 2, false);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Customer_cannot_cancel_a_confirmed_order()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 1)));
        await _svc.UpdateStatusAsync(order.Id, "Confirmed", 99, true);

        var act = () => _svc.UpdateStatusAsync(order.Id, "Cancelled", 1, false);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Customer_cannot_view_someone_elses_order()
    {
        var (order, _) = await _svc.PlaceOrderAsync(1, null, Req((1, 1)));

        var act = () => _svc.GetByIdAsync(order.Id, 2, false);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
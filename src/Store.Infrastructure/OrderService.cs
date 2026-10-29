using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Store.Application.DTOs;
using Store.Application.Exceptions;
using Store.Application.Interfaces;
using Store.Application.Services;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Data;

namespace Store.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly StoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrderService> _logger;

    public OrderService(StoreDbContext db, IMemoryCache cache, ILogger<OrderService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    // ---------------- PLACE ORDER (Rules 1, 2, 3, 5) ----------------
    public async Task<(OrderDto Order, bool Created)> PlaceOrderAsync(
        int customerId, string? idempotencyKey, CreateOrderRequest request, CancellationToken ct = default)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (key is { Length: > 100 })
            throw new ValidationException("Idempotency-Key must be 100 characters or less.");
        if (request.Items is null || request.Items.Count == 0)
            throw new ValidationException("Order must contain at least one item.");

        // Rule 3: same key + same user => return the original order
        if (key is not null)
        {
            var existing = await FindByKeyAsync(key, customerId, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent replay for key {Key}, order {OrderId}", key, existing.Id);
                return (existing, false);
            }
        }

        // same product listed twice => merge quantities
        var lines = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();
        var ids = lines.Select(l => l.ProductId).ToList();

        try
        {
            // Rule 1: everything below is ONE transaction. Any exception => automatic rollback.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            var order = new Order { CustomerId = customerId, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow };
            decimal subTotal = 0;

            foreach (var (productId, qty) in lines)
            {
                if (!products.TryGetValue(productId, out var product) || !product.IsActive)
                    throw new NotFoundException($"Product {productId} not found.");

                if (product.StockQuantity < qty)
                    throw new ConflictException(
                        $"Insufficient stock for '{product.Name}'. Available: {product.StockQuantity}, requested: {qty}.");

                product.StockQuantity -= qty; // RowVersion makes this safe (Rule 2)
                order.Items.Add(new OrderItem { ProductId = productId, Quantity = qty, UnitPrice = product.Price });
                subTotal += product.Price * qty;
            }

            // Rule 5: server-side discount. This will be the customer's (count + 1)th order.
            var previousOrders = await _db.Orders.CountAsync(o => o.CustomerId == customerId, ct);
            var (discount, total) = DiscountCalculator.Calculate(subTotal, previousOrders + 1);
            order.SubTotal = Math.Round(subTotal, 2);
            order.Discount = discount;
            order.Total = total;

            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct); // UPDATE Products ... WHERE RowVersion = @old  (throws if someone else won)

            if (key is not null)
            {
                _db.IdempotencyRecords.Add(new IdempotencyRecord { Key = key, UserId = customerId, OrderId = order.Id });
                await _db.SaveChangesAsync(ct); // unique index (Key, UserId) is the final safety net
            }

            await tx.CommitAsync(ct);
            InvalidateProductCache();

            _logger.LogInformation("Order {OrderId} placed by {CustomerId}. Total {Total}", order.Id, customerId, total);
            return (await GetOrderDtoAsync(order.Id, ct), true);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Rule 2: another request changed the same product row first => 409, not 500
            _logger.LogWarning("Concurrency conflict while placing order for customer {CustomerId}", customerId);
            throw new ConflictException("Stock changed while placing your order (item may be sold out). Please try again.");
        }
        catch (DbUpdateException) when (key is not null)
        {
            // two parallel requests with the same Idempotency-Key: the other one won, return its order
            _db.ChangeTracker.Clear();
            var existing = await FindByKeyAsync(key, customerId, ct);
            if (existing is not null) return (existing, false);
            throw;
        }
    }

    // ---------------- READ ----------------
    public Task<PagedResult<OrderDto>> GetMyOrdersAsync(int customerId, int page, int pageSize, CancellationToken ct = default)
        => PageAsync(_db.Orders.Where(o => o.CustomerId == customerId), page, pageSize, ct);

    public Task<PagedResult<OrderDto>> GetAllOrdersAsync(int page, int pageSize, CancellationToken ct = default)
        => PageAsync(_db.Orders, page, pageSize, ct);

    public async Task<OrderDto> GetByIdAsync(int orderId, int requesterId, bool isAdmin, CancellationToken ct = default)
    {
        var dto = await GetOrderDtoAsync(orderId, ct);
        if (!isAdmin && dto.CustomerId != requesterId)
            throw new ForbiddenException("You can only view your own orders.");
        return dto;
    }

    // ---------------- STATUS CHANGE (Rule 4) ----------------
    public async Task<OrderDto> UpdateStatusAsync(
        int orderId, string newStatus, int requesterId, bool isAdmin, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var target) || !Enum.IsDefined(target))
            throw new ValidationException("Invalid status. Allowed: Pending, Confirmed, Shipped, Cancelled.");

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException($"Order {orderId} not found.");

        if (!isAdmin)
        {
            if (order.CustomerId != requesterId)
                throw new ForbiddenException("You can only modify your own orders.");
            if (target != OrderStatus.Cancelled)
                throw new ForbiddenException("Customers can only cancel orders.");
            if (order.Status != OrderStatus.Pending)
                throw new ValidationException("Customers can only cancel orders that are Pending.");
        }

        if (!OrderStatusRules.CanTransition(order.Status, target))
            throw new ValidationException($"Invalid status transition: {order.Status} to {target}.");

        await ApplyTransitionAsync(orderId, order.Status, target, ct);
        return await GetOrderDtoAsync(orderId, ct);
    }

    // ---------------- BACKGROUND CLEANUP (Rule 6) ----------------
    public async Task<int> CancelExpiredPendingOrdersAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var ids = await _db.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .Take(100)
            .ToListAsync(ct);

        var cancelled = 0;
        foreach (var id in ids)
        {
            try
            {
                await ApplyTransitionAsync(id, OrderStatus.Pending, OrderStatus.Cancelled, ct);
                cancelled++;
            }
            catch (ConflictException ex)
            {
                // someone changed it meanwhile; next run will retry
                _logger.LogWarning("Skipped auto-cancel of order {OrderId}: {Message}", id, ex.Message);
            }
        }
        return cancelled;
    }

    // ---------------- helpers ----------------

    // Atomic status change: "UPDATE Orders SET Status=@to WHERE Id=@id AND Status=@from".
    // If 0 rows are updated, someone else already changed it, so stock is never restored twice.
    private async Task ApplyTransitionAsync(int orderId, OrderStatus from, OrderStatus to, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var rows = await _db.Orders
            .Where(o => o.Id == orderId && o.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, to), ct);

        if (rows == 0)
            throw new ConflictException("Order status was changed by someone else. Please reload and try again.");

        if (to == OrderStatus.Cancelled)
        {
            var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
            var productIds = items.Select(i => i.ProductId).ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in items)
                products[item.ProductId].StockQuantity += item.Quantity; // restore stock

            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("Stock was changing while cancelling. Please try again.");
            }
        }

        await tx.CommitAsync(ct);
        if (to == OrderStatus.Cancelled) InvalidateProductCache();
        _logger.LogInformation("Order {OrderId} status {From} -> {To}", orderId, from, to);
    }

    private async Task<OrderDto?> FindByKeyAsync(string key, int userId, CancellationToken ct)
    {
        var orderId = await _db.IdempotencyRecords.AsNoTracking()
            .Where(r => r.Key == key && r.UserId == userId)
            .Select(r => (int?)r.OrderId)
            .FirstOrDefaultAsync(ct);

        return orderId is null ? null : await GetOrderDtoAsync(orderId.Value, ct);
    }

    private async Task<OrderDto> GetOrderDtoAsync(int id, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException($"Order {id} not found.");
        return ToDto(order);
    }

    private static async Task<PagedResult<OrderDto>> PageInternalAsync(
        IQueryable<Order> q, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var total = await q.CountAsync(ct);
        var orders = await q
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsNoTracking()
            .ToListAsync(ct); // one query with joins, no N+1

        return new PagedResult<OrderDto>
        {
            Items = orders.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private Task<PagedResult<OrderDto>> PageAsync(IQueryable<Order> q, int page, int pageSize, CancellationToken ct)
        => PageInternalAsync(q, page, pageSize, ct);

    private void InvalidateProductCache()
    {
        var v = _cache.GetOrCreate(ProductService.CacheVersionKey, _ => 0);
        _cache.Set(ProductService.CacheVersionKey, v + 1);
    }

    private static OrderDto ToDto(Order o) => new()
    {
        Id = o.Id,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.FullName ?? string.Empty,
        Status = o.Status.ToString(),
        SubTotal = o.SubTotal,
        Discount = o.Discount,
        Total = o.Total,
        CreatedAt = o.CreatedAt,
        Items = o.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };
}
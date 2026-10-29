using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Store.Application.DTOs;
using Store.Infrastructure.Data;
using Xunit;

namespace Store.Tests;

public class StoreApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnection =
        "Server=localhost\\SQLEXPRESS;Database=StoreDb_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    public StoreApiFactory()
    {
        // environment variables are visible to Program.cs before the host is built
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestConnection);
        Environment.SetEnvironmentVariable("Jwt__Key", "IntegrationTestOnlySecretKey_NotUsedAnywhereElse_123456");

        // start every run with a fresh database (the app migrates + seeds it on startup)
        using var db = new StoreDbContext(
            new DbContextOptionsBuilder<StoreDbContext>().UseSqlServer(TestConnection).Options);
        db.Database.EnsureDeleted();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        using var db = new StoreDbContext(
            new DbContextOptionsBuilder<StoreDbContext>().UseSqlServer(TestConnection).Options);
        db.Database.EnsureDeleted();
    }
}

// Rule 2: two customers buy the last unit at the same time => one 201, one 409
public class ConcurrencyIntegrationTests : IClassFixture<StoreApiFactory>
{
    private readonly StoreApiFactory _factory;

    public ConcurrencyIntegrationTests(StoreApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "Customer@123" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<HttpResponseMessage> PlaceOrderAsync(
        HttpClient client, string key, int productId, Task startSignal)
    {
        await startSignal; // both requests are released at the same moment
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest
            {
                Items = new() { new OrderItemRequest { ProductId = productId, Quantity = 1 } }
            })
        };
        req.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task Two_parallel_orders_for_the_last_unit_give_one_201_and_one_409()
    {
        const int lastUnitProductId = 15; // SKU-015, seeded with StockQuantity = 1

        var client1 = await LoginAsync("customer1@store.com");
        var client2 = await LoginAsync("customer2@store.com");

        var start = new TaskCompletionSource();
        var t1 = Task.Run(() => PlaceOrderAsync(client1, "race-1", lastUnitProductId, start.Task));
        var t2 = Task.Run(() => PlaceOrderAsync(client2, "race-2", lastUnitProductId, start.Task));

        start.SetResult(); // go!
        var responses = await Task.WhenAll(t1, t2);

        var codes = responses.Select(r => r.StatusCode).ToList();
        codes.Should().Contain(HttpStatusCode.Created);
        codes.Should().Contain(HttpStatusCode.Conflict);
        codes.Should().NotContain(HttpStatusCode.InternalServerError);

        // exactly one unit was sold, stock must be 0 (never negative)
        var product = await client1.GetFromJsonAsync<ProductDto>($"/api/products/{lastUnitProductId}");
        product!.StockQuantity.Should().Be(0);
    }
}
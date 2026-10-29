# Store App: Order & Inventory API + Angular UI

Built on **.NET 10**, ASP.NET Core Web API, EF Core (SQL Server) and **Angular 21**.

## Setup and run

Requirements: .NET SDK 10, Node 20+, SQL Server Express (`localhost\SQLEXPRESS`), Angular CLI.

1. **JWT key (not committed):**
   ```
   dotnet user-secrets init --project src/Store.Api
   dotnet user-secrets set "Jwt:Key" "<any long random string, 32+ chars>" --project src/Store.Api
   ```
2. **API:** `dotnet run --project src/Store.Api` (http://localhost:5096, Swagger at /swagger).
   On startup the app applies migrations, creates `StoreDb` and seeds data.
3. **UI:** `cd store-ui && npm install && ng serve` (http://localhost:4200).

If your SQL Server instance differs, change `ConnectionStrings:DefaultConnection` in `src/Store.Api/appsettings.json`.

## Migrations and tests

- Migrations run automatically at startup. Manually: `dotnet ef database update --project src/Store.Infrastructure --startup-project src/Store.Api`
- New migration: `dotnet ef migrations add <Name> --project src/Store.Infrastructure --startup-project src/Store.Api --output-dir Migrations`
- Tests: `dotnet test` (37 tests; the integration test uses a separate database `StoreDb_Test` on SQL Server Express and deletes it afterwards).

## Seeded logins

| Role | Email | Password |
|---|---|---|
| Admin | admin@store.com | Admin@123 |
| Customer | customer1@store.com, customer2@store.com, customer3@store.com | Customer@123 |

SKU-015 has stock 1 on purpose (last-unit concurrency demo).

## Architecture

- **Store.Domain**: entities and enums, no dependencies.
- **Store.Application**: DTOs, service interfaces, exceptions, pure rules (`DiscountCalculator`, `OrderStatusRules`).
- **Store.Infrastructure**: EF Core DbContext, migrations, seeding, services (auth, products, orders, reports), JWT, background service.
- **Store.Api**: controllers, JWT auth, rate limiting, Swagger, Serilog, global exception handler (ProblemDetails).
- **store-ui**: Angular standalone components, signals, service layer, JWT interceptor, route guards.

## How concurrency is solved (Rule 2)

`Product.RowVersion` is a SQL Server `rowversion`. Stock is decremented inside a transaction, and EF issues `UPDATE ... WHERE RowVersion = @old`. If another request changed the row first, 0 rows are updated, EF throws `DbUpdateConcurrencyException`, and the service converts it to `ConflictException`, which the global handler returns as **HTTP 409** (never 500). If the second request reads stock after the first committed, "insufficient stock" also returns 409. Proven by the integration test (one 201, one 409).

## How idempotency is solved (Rule 3)

`POST /api/orders` reads the `Idempotency-Key` header. An `IdempotencyRecords` table stores (Key, UserId, OrderId) with a **unique index on (Key, UserId)**. A repeated key returns the original order (HTTP 200) with no new stock change. Two parallel requests with the same key are protected by the unique index; the loser returns the winner's order. The Angular checkout keeps one key per cart until the order succeeds, and disables the button while a request is running.

## Other rules

- **Rule 1:** the whole order is one DB transaction; any failure rolls back all stock changes.
- **Rule 4:** `OrderStatusRules`; invalid transitions return 400; cancelling restores stock using an atomic `UPDATE ... WHERE Status = @from`, so stock is never restored twice.
- **Rule 5:** server-side, `decimal`, rounded to 2 places.
- **Rule 6:** `PendingOrderCleanupService` runs every minute and cancels orders Pending for more than 30 minutes.

## Part B: generated SQL for top customers (LINQ)

```sql
-- Logged by EF Core (Executed DbCommand) for GET /api/reports/top-customers?days=30
-- Parameters: @p = 5 (top N), @since = UTC now minus 30 days
SELECT TOP(@p) [o].[CustomerId], [u].[FullName], [u].[Email], COALESCE(SUM([o].[Total]), 0.0) AS [TotalSpend], COUNT(*) AS [OrderCount]
FROM [Orders] AS [o]
INNER JOIN [Users] AS [u] ON [o].[CustomerId] = [u].[Id]
WHERE [o].[CreatedAt] >= @since AND [o].[Status] IN (N'Confirmed', N'Shipped')
GROUP BY [o].[CustomerId], [u].[FullName], [u].[Email]
ORDER BY COALESCE(SUM([o].[Total]), 0.0) DESC
```

It is a single query (join, group, sort, top), with no N+1 and nothing loaded into memory. The other recurring `SELECT TOP(@p) [o].[Id] ... Status = N'Pending'` line in the logs is the Rule 6 background cleanup running every minute.

## Assumptions

- Discounts are applied **sequentially**: 5% first when SubTotal > 10,000, then an extra 10% on the discounted amount for every 5th order of the customer.
- Revenue and top customers count only **Confirmed + Shipped** orders.
- Insufficient stock returns **409** (same as a concurrency conflict) so the client handles both the same way.
- Customers register only as Customer; the role is never taken from the client.
- Query 4 uses `ROW_NUMBER`, so equal totals still count as separate orders.
- Part C: the email contained no code, so REVIEW.md reviews common production problems in this domain with corrected code.

## Not done / what I would improve

- Bonus items not done: Docker Compose, SignalR, outbox pattern, frontend unit tests.
- Unit tests use SQLite where RowVersion is a plain token; real concurrency is tested on SQL Server.
- Add a refresh-token cleanup job, structured log sinks (Seq), and more integration tests.
- Move the access token to an HttpOnly cookie instead of localStorage.
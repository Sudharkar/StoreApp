# REVIEW.md

The email did not include the code for Part C. Below are the most common problems that "work in testing but fail in production" in an order/inventory API, each with a corrected version.

## 1. Stock check and update without a transaction or concurrency token (overselling)

Problem: read stock, check, then save. Two requests both read `1` and both sell it.

```csharp
// BAD
var p = await db.Products.FindAsync(id);
if (p.StockQuantity >= qty) { p.StockQuantity -= qty; await db.SaveChangesAsync(); }
```

Fix: `RowVersion` concurrency token, a transaction, and a 409 on conflict.

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);
var p = await db.Products.FirstAsync(x => x.Id == id, ct);
if (p.StockQuantity < qty) throw new ConflictException("Insufficient stock.");
p.StockQuantity -= qty;
try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
catch (DbUpdateConcurrencyException) { throw new ConflictException("Stock changed, try again."); }
```

## 2. No idempotency on POST

Problem: a double click or retry creates two orders. Fix: `Idempotency-Key` header stored with a unique index on (Key, UserId); return the original order on repeat.

## 3. N+1 queries and in-memory filtering

```csharp
// BAD
var all = await db.Orders.ToListAsync();
foreach (var o in all) o.Items = await db.OrderItems.Where(i => i.OrderId == o.Id).ToListAsync();
```

Problem: loads every row and runs one query per order. Fix: filter, group and page in SQL with `IQueryable`, `Include` or a projection, ending with one `ToListAsync`.

## 4. Blocking calls and `async void`

```csharp
// BAD
var x = service.GetAsync().Result;   // deadlocks / thread starvation
async void Handle() { ... }          // exceptions crash the process
```

Fix: `await` everywhere, return `Task`, pass `CancellationToken`.

## 5. Secrets hardcoded

Problem: JWT key and passwords in source control. Fix: user-secrets or environment variables.

## 6. Client-trusted price, discount and role

Problem: accepting `Price`, `Discount` or `Role` from the request lets users buy for 1 rupee or become Admin. Fix: accept only ProductId and Quantity; calculate on the server; set role on the server.

## 7. Money as float/double

Problem: rounding errors. Fix: `decimal` with `HasPrecision(18,2)` and `Math.Round(x, 2, MidpointRounding.AwayFromZero)`.

## 8. Exceptions leaked and swallowed

Problem: stack traces returned on HTTP 500, and a background loop that dies on the first exception. Fix: global `IExceptionHandler` returning `ProblemDetails`; try/catch with logging inside the loop.

## 9. Scoped DbContext inside a singleton / BackgroundService

Problem: throws or shares one context across threads. Fix: inject `IServiceScopeFactory` and create a scope per run.

## 10. Missing authorization checks (IDOR)

Problem: `GET /orders/{id}` returns any order to any logged-in user. Fix: compare `order.CustomerId` with the token's user id unless Admin; return 403.

## 11. Weak password storage, no login throttling

Fix: BCrypt hash, same error for wrong email or password, rate limit login (5 per minute per IP).

## 12. Cache never invalidated

Problem: stale stock in the cached product list. Fix: short expiry plus a version key bumped on create, update and stock change.
using Microsoft.EntityFrameworkCore;
using Store.Application.DTOs;
using Store.Application.Interfaces;
using Store.Domain.Enums;
using Store.Infrastructure.Data;

namespace Store.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly StoreDbContext _db;

    public ReportService(StoreDbContext db) => _db = db;

    // Top customers by spend (Confirmed + Shipped) in the last N days.
    // One SQL query: JOIN + GROUP BY + ORDER BY + TOP, no N+1, nothing loaded in memory.
    public async Task<List<TopCustomerDto>> GetTopCustomersAsync(int days, int top, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 3650);
        top = Math.Clamp(top, 1, 100);
        var since = DateTime.UtcNow.AddDays(-days);

        return await _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= since &&
                        (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped))
            .GroupBy(o => new { o.CustomerId, o.Customer!.FullName, o.Customer.Email })
            .Select(g => new TopCustomerDto
            {
                CustomerId = g.Key.CustomerId,
                FullName = g.Key.FullName,
                Email = g.Key.Email,
                TotalSpend = g.Sum(x => x.Total),
                OrderCount = g.Count()
            })
            .OrderByDescending(x => x.TotalSpend)
            .Take(top)
            .ToListAsync(ct);
    }
}
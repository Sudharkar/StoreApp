using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Store.Application.Interfaces;

namespace Store.Infrastructure.Background;

// Rule 6: every minute, cancel orders Pending for more than 30 minutes and restore stock.
public class PendingOrderCleanupService : BackgroundService
{
    private static readonly TimeSpan MaxPendingAge = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingOrderCleanupService> _logger;

    public PendingOrderCleanupService(IServiceScopeFactory scopeFactory, ILogger<PendingOrderCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                // BackgroundService is a singleton, DbContext is scoped => create a scope each run
                using var scope = _scopeFactory.CreateScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var count = await orders.CancelExpiredPendingOrdersAsync(MaxPendingAge, stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Auto-cancelled {Count} expired pending orders", count);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // never let the loop die
                _logger.LogError(ex, "Pending order cleanup failed");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
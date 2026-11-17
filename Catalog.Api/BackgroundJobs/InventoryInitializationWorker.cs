namespace Catalog.Api.BackgroundJobs;

using Catalog.Application.Services.Product;

/// <summary>
/// Retries the inventory row creation in FulfillmentService for SKUs left Pending (approval or new SKU while Fulfillment was down).
/// Work lives in the database, so it survives a restart. Set InventoryInitialization:Enabled to false to turn the worker off.
/// </summary>
public sealed class InventoryInitializationWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InventoryInitializationWorker> _logger;

    public InventoryInitializationWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<InventoryInitializationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("InventoryInitialization:Enabled", true))
        {
            _logger.LogInformation("Inventory initialization worker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var initializer = scope.ServiceProvider.GetRequiredService<IProductInventoryInitializer>();
                var processed = await initializer.ProcessDueAsync(BatchSize, stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("Inventory initialization worker attempted {Count} SKU(s).", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad cycle (database down, for example) must not stop the worker.
                _logger.LogError(ex, "Inventory initialization cycle failed. It will run again in {Interval}.", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

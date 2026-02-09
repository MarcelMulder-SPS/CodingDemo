using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Services;
using Microsoft.Extensions.Options;

namespace InventoryAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly InventoryOrchestrator _orchestrator;
    private readonly ISpoolStore _spoolStore;
    private readonly IUploadCoordinator _uploadCoordinator;
    private readonly InventoryOptions _options;

    public Worker(
        ILogger<Worker> logger,
        InventoryOrchestrator orchestrator,
        ISpoolStore spoolStore,
        IUploadCoordinator uploadCoordinator,
        IOptions<InventoryOptions> options)
    {
        _logger = logger;
        _orchestrator = orchestrator;
        _spoolStore = spoolStore;
        _uploadCoordinator = uploadCoordinator;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inventory Agent Worker starting");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CollectionIntervalSeconds));

        // Run first collection immediately
        await RunCollectionCycleAsync(stoppingToken);

        // Then run on interval
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCollectionCycleAsync(stoppingToken);
        }

        _logger.LogInformation("Inventory Agent Worker stopping");
    }

    private async Task RunCollectionCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting inventory collection cycle");

            // Collect inventory
            var report = await _orchestrator.CollectInventoryAsync(cancellationToken);

            // Spool to disk
            await _spoolStore.EnqueueAsync(report, cancellationToken);
            _logger.LogInformation("Report spooled successfully");

            // Attempt upload
            await _uploadCoordinator.FlushSpoolAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inventory collection cycle");
        }
    }
}


using InventoryAgent.Contracts.Interfaces;
using InventoryAgent.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryAgent.Services;

/// <summary>
/// Orchestrates the parallel collection of inventory data from all registered collectors.
/// </summary>
public class InventoryOrchestrator
{
    private readonly IEnumerable<ICollector> _collectors;
    private readonly InventoryOptions _options;
    private readonly ILogger<InventoryOrchestrator> _logger;

    public InventoryOrchestrator(
        IEnumerable<ICollector> collectors,
        IOptions<InventoryOptions> options,
        ILogger<InventoryOrchestrator> logger)
    {
        _collectors = collectors;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InventoryReport> CollectInventoryAsync(CancellationToken cancellationToken)
    {
        var hostname = Environment.MachineName;
        var timestamp = DateTime.UtcNow;
        var warnings = new List<CollectionWarning>();

        _logger.LogInformation("Starting inventory collection for {Hostname}", hostname);

        // Collect from all collectors
        var collectionTasks = _collectors.Select(async collector =>
        {
            try
            {
                _logger.LogDebug("Collecting from {CollectorName}", collector.Name);
                return await collector.CollectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Collector {CollectorName} failed", collector.Name);
                var warning = new CollectionWarning(
                    collector.Name,
                    "All",
                    "Collector failed",
                    ex.Message);
                return (Data: (object?)null, Warnings: new[] { warning } as IReadOnlyList<CollectionWarning>);
            }
        });

        var results = _options.EnableParallelCollection
            ? await Task.WhenAll(collectionTasks)
            : await Task.WhenAll(collectionTasks.Select(async t => await t));

        // Aggregate results
        HardwareInfo? hardware = null;
        SoftwareInfo? software = null;

        foreach (var (data, collectorWarnings) in results)
        {
            if (collectorWarnings != null)
            {
                warnings.AddRange(collectorWarnings);
            }

            if (data is HardwareInfo hw)
            {
                hardware = hw;
            }
            else if (data is SoftwareInfo sw)
            {
                software = sw;
            }
        }

        _logger.LogInformation("Inventory collection completed with {WarningCount} warnings", warnings.Count);

        return new InventoryReport(
            timestamp,
            hostname,
            hardware,
            software,
            warnings);
    }
}

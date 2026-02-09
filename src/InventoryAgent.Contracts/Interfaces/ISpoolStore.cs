using InventoryAgent.Contracts.Models;

namespace InventoryAgent.Contracts.Interfaces;

/// <summary>
/// Interface for spooling inventory reports to disk.
/// </summary>
public interface ISpoolStore
{
    Task EnqueueAsync(InventoryReport report, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryReport>> DequeueAsync(int maxCount, CancellationToken cancellationToken);
    Task<int> GetQueuedCountAsync(CancellationToken cancellationToken);
}

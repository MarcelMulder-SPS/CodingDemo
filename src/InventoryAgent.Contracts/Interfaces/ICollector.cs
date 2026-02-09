using InventoryAgent.Contracts.Models;

namespace InventoryAgent.Contracts.Interfaces;

/// <summary>
/// Base interface for all collectors.
/// </summary>
public interface ICollector
{
    string Name { get; }
    Task<(object? Data, IReadOnlyList<CollectionWarning> Warnings)> CollectAsync(CancellationToken cancellationToken);
}

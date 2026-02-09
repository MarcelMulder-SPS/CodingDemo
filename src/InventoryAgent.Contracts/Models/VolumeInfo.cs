namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Volume/partition information.
/// </summary>
public record VolumeInfo(
    string? Name,
    string? FileSystem,
    long? TotalBytes,
    long? FreeBytes,
    long? UsedBytes);

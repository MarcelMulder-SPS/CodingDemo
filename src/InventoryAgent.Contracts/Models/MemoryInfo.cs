namespace InventoryAgent.Contracts.Models;

/// <summary>
/// System memory information.
/// </summary>
public record MemoryInfo(
    long? TotalBytes,
    IReadOnlyList<MemoryModuleInfo>? Modules);

/// <summary>
/// Individual memory module information.
/// </summary>
public record MemoryModuleInfo(
    long? CapacityBytes,
    string? Manufacturer,
    string? PartNumber,
    int? SpeedMhz);

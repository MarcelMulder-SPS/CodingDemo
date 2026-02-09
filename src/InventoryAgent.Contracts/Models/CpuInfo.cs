namespace InventoryAgent.Contracts.Models;

/// <summary>
/// CPU hardware information.
/// </summary>
public record CpuInfo(
    string? Model,
    string? Vendor,
    int? PhysicalCores,
    int? LogicalProcessors,
    string? Architecture,
    double? FrequencyMhz);

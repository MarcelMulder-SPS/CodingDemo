namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Display monitor information.
/// </summary>
public record MonitorInfo(
    string? Name,
    string? Manufacturer,
    string? Model,
    int? WidthPixels,
    int? HeightPixels);

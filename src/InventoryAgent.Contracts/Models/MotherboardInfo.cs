namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Motherboard/baseboard information.
/// </summary>
public record MotherboardInfo(
    string? Manufacturer,
    string? Product,
    string? Version,
    string? SerialNumber);

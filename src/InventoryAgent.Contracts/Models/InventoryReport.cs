namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Complete inventory report for a machine.
/// </summary>
public record InventoryReport(
    DateTime Timestamp,
    string Hostname,
    HardwareInfo? Hardware,
    SoftwareInfo? Software,
    IReadOnlyList<CollectionWarning>? Warnings);

namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Physical disk information.
/// </summary>
public record DiskInfo(
    string? Model,
    long? SizeBytes,
    string? InterfaceType,
    string? SerialNumber);

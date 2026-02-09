namespace InventoryAgent.Contracts.Models;

/// <summary>
/// GPU hardware information.
/// </summary>
public record GpuInfo(
    string? Model,
    string? Vendor,
    string? DriverVersion,
    long? VramBytes);

namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Network adapter information.
/// </summary>
public record NetworkAdapterInfo(
    string? Name,
    string? MacAddress,
    IReadOnlyList<string>? IPv4Addresses,
    IReadOnlyList<string>? IPv6Addresses,
    long? LinkSpeedBps);

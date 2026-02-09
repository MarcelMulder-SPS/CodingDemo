namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Operating system information.
/// </summary>
public record OsInfo(
    string? Name,
    string? Version,
    string? Build,
    string? KernelVersion,
    string? Architecture,
    DateTime? BootTime,
    TimeSpan? Uptime);

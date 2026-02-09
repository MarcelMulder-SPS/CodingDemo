namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Installed application information.
/// </summary>
public record InstalledApp(
    string? Name,
    string? Version,
    string? Publisher,
    DateTime? InstallDate);

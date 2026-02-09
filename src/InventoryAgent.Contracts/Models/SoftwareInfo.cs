namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Aggregated software information.
/// </summary>
public record SoftwareInfo(
    OsInfo? Os,
    IReadOnlyList<InstalledApp>? InstalledApps,
    IReadOnlyList<ProcessInfo>? RunningProcesses);

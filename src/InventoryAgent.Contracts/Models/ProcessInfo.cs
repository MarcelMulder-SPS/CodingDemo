namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Running process information.
/// </summary>
public record ProcessInfo(
    int? ProcessId,
    string? Name,
    string? Path,
    string? UserName,
    long? MemoryBytes);

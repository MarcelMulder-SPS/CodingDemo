namespace InventoryAgent.Contracts.Models;

/// <summary>
/// BIOS/UEFI information.
/// </summary>
public record BiosInfo(
    string? Vendor,
    string? Version,
    DateTime? ReleaseDate);

namespace InventoryAgent.Contracts.Models;

/// <summary>
/// Represents a warning about data that could not be collected.
/// </summary>
public record CollectionWarning(
    string CollectorName,
    string Field,
    string Message,
    string? ExceptionSummary = null);

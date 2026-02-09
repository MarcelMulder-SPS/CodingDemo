namespace InventoryAgent.Contracts.Interfaces;

/// <summary>
/// Interface for coordinating the upload of spooled reports.
/// </summary>
public interface IUploadCoordinator
{
    Task FlushSpoolAsync(CancellationToken cancellationToken);
}

namespace sssync_backend.core.Interfaces;

public interface ISyncService
{
    /// <summary>
    /// Performs a full synchronization for a given user across their connected platforms.
    /// </summary>
    /// <param name="userId">The ID of the user to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task SynchronizeUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a sync specifically for a single product across platforms.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="internalProductId">The internal sssync product ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task SynchronizeProductAsync(string userId, Guid internalProductId, CancellationToken cancellationToken = default);

    // Add methods for migrating data, handling specific webhooks, etc. later
} 
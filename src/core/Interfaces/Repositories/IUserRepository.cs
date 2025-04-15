namespace sssync_backend.core.Interfaces.Repositories;

public interface IUserRepository
{
    /// <summary>
    /// Gets a list of User IDs that have at least one active platform connection.
    /// </summary>
    Task<IEnumerable<string>> GetUserIdsWithActiveSyncAsync(CancellationToken cancellationToken = default);

    // Add other user-related methods if needed (e.g., GetUserDetails)
} 
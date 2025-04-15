using sssync_backend.core.Models; // Assuming PlatformConnection is in Models

namespace sssync_backend.core.Interfaces.Repositories;

public interface IPlatformConnectionRepository
{
    /// <summary>
    /// Gets all active platform connections for a specific user.
    /// </summary>
    Task<IEnumerable<PlatformConnection>> GetActiveConnectionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific platform connection for a user.
    /// </summary>
    Task<PlatformConnection?> GetConnectionAsync(string userId, string platformName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a platform connection (based on UserId and Platform).
    /// Ensures sensitive tokens are handled securely.
    /// </summary>
    Task SaveConnectionAsync(PlatformConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a platform connection for a user.
    /// </summary>
    Task DeleteConnectionAsync(string userId, string platformName, CancellationToken cancellationToken = default);
} 
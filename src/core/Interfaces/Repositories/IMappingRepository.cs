namespace sssync_backend.core.Interfaces.Repositories;

public interface IMappingRepository
{
    /// <summary>
    /// Gets the platform-specific ID for a given internal ID and entity type.
    /// </summary>
    Task<string?> GetPlatformIdAsync(Guid internalId, string platformName, string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the internal ID for a given platform-specific ID and entity type.
    /// </summary>
    Task<Guid?> GetInternalIdAsync(string platformId, string platformName, string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a mapping between an internal ID and a platform ID.
    /// </summary>
    Task SaveMappingAsync(Guid internalId, string platformName, string entityType, string platformId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a platform-specific metadata value associated with an internal ID (e.g., Shopify InventoryItemId).
    /// </summary>
    Task<string?> GetPlatformMetaValueAsync(Guid internalId, string platformName, string entityType, string metaKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a platform-specific metadata value associated with an internal ID.
    /// </summary>
    Task SavePlatformMetaValueAsync(Guid internalId, string platformName, string entityType, string metaKey, string metaValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all mappings for a specific internal ID.
    /// </summary>
    Task<Dictionary<string, string>> GetPlatformMappingsAsync(Guid internalId, string entityType, CancellationToken cancellationToken = default);
} 
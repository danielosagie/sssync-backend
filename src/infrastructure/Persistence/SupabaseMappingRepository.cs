using Supabase;
using sssync_backend.core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Postgrest.Exceptions;

namespace sssync_backend.infrastructure.Persistence;

// Define a simple model matching your mapping table structure
public class PlatformIdMapping
{
    // Adjust property names and types based on your Supabase table columns
    public Guid internal_id { get; set; }
    public string platform_name { get; set; } = string.Empty;
    public string entity_type { get; set; } = string.Empty;
    public string platform_id { get; set; } = string.Empty;
    public string? meta_key { get; set; } // For storing things like InventoryItemId
    public string? meta_value { get; set; }
    public DateTimeOffset updated_at { get; set; } = DateTimeOffset.UtcNow;
}


public class SupabaseMappingRepository : SupabaseRepositoryBase, IMappingRepository
{
    public SupabaseMappingRepository(Client supabaseClient, ILogger<SupabaseMappingRepository> logger)
        : base(supabaseClient, logger) { }

    public async Task<string?> GetPlatformIdAsync(Guid internalId, string platformName, string entityType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _supabaseClient.From<PlatformIdMapping>(MappingsTable)
                .Select(m => new { m.platform_id }) // Select only the needed column
                .Filter("internal_id", Postgrest.Constants.Operator.Equals, internalId.ToString())
                .Filter("platform_name", Postgrest.Constants.Operator.Equals, platformName)
                .Filter("entity_type", Postgrest.Constants.Operator.Equals, entityType)
                .Filter("meta_key", Postgrest.Constants.Operator.IsNull, null) // Ensure we get the main mapping, not meta
                .Limit(1)
                .Get(cancellationToken);

            return response.Models?.FirstOrDefault()?.platform_id;
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error getting platform ID for InternalId {InternalId}, Platform {Platform}, Type {Type}", internalId, platformName, entityType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error getting platform ID for InternalId {InternalId}, Platform {Platform}, Type {Type}", internalId, platformName, entityType);
            throw;
        }
    }

    public async Task<Guid?> GetInternalIdAsync(string platformId, string platformName, string entityType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _supabaseClient.From<PlatformIdMapping>(MappingsTable)
                .Select(m => new { m.internal_id })
                .Filter("platform_id", Postgrest.Constants.Operator.Equals, platformId)
                .Filter("platform_name", Postgrest.Constants.Operator.Equals, platformName)
                .Filter("entity_type", Postgrest.Constants.Operator.Equals, entityType)
                .Filter("meta_key", Postgrest.Constants.Operator.IsNull, null)
                .Limit(1)
                .Get(cancellationToken);

            return response.Models?.FirstOrDefault()?.internal_id;
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error getting internal ID for PlatformId {PlatformId}, Platform {Platform}, Type {Type}", platformId, platformName, entityType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error getting internal ID for PlatformId {PlatformId}, Platform {Platform}, Type {Type}", platformId, platformName, entityType);
            throw;
        }
    }

    public async Task SaveMappingAsync(Guid internalId, string platformName, string entityType, string platformId, CancellationToken cancellationToken = default)
    {
        var mapping = new PlatformIdMapping
        {
            internal_id = internalId,
            platform_name = platformName,
            entity_type = entityType,
            platform_id = platformId,
            updated_at = DateTimeOffset.UtcNow,
            meta_key = null, // Explicitly null for main mapping
            meta_value = null
        };

        try
        {
            // Upsert based on internal_id, platform_name, entity_type, and meta_key (being null)
            var response = await _supabaseClient.From<PlatformIdMapping>(MappingsTable)
                .Upsert(mapping, new Postgrest.Models.UpsertOptions { OnConflict = "internal_id, platform_name, entity_type, meta_key" }, cancellationToken);

            response.ResponseMessage?.EnsureSuccessStatusCode();
            _logger.LogDebug("Saved mapping: InternalId {InternalId} <-> Platform {Platform} {Type} ID {PlatformId}", internalId, platformName, entityType, platformId);
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error saving mapping for InternalId {InternalId}, Platform {Platform}, Type {Type}", internalId, platformName, entityType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error saving mapping for InternalId {InternalId}, Platform {Platform}, Type {Type}", internalId, platformName, entityType);
            throw;
        }
    }

     public async Task<string?> GetPlatformMetaValueAsync(Guid internalId, string platformName, string entityType, string metaKey, CancellationToken cancellationToken = default)
    {
         try
        {
            var response = await _supabaseClient.From<PlatformIdMapping>(MappingsTable)
                .Select(m => new { m.meta_value })
                .Filter("internal_id", Postgrest.Constants.Operator.Equals, internalId.ToString())
                .Filter("platform_name", Postgrest.Constants.Operator.Equals, platformName)
                .Filter("entity_type", Postgrest.Constants.Operator.Equals, entityType)
                .Filter("meta_key", Postgrest.Constants.Operator.Equals, metaKey) // Match the specific meta key
                .Limit(1)
                .Get(cancellationToken);

            return response.Models?.FirstOrDefault()?.meta_value;
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error getting meta value {MetaKey} for InternalId {InternalId}, Platform {Platform}, Type {Type}", metaKey, internalId, platformName, entityType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error getting meta value {MetaKey} for InternalId {InternalId}, Platform {Platform}, Type {Type}", metaKey, internalId, platformName, entityType);
            throw;
        }
    }

    public async Task SavePlatformMetaValueAsync(Guid internalId, string platformName, string entityType, string metaKey, string metaValue, CancellationToken cancellationToken = default)
    {
         var mapping = new PlatformIdMapping
        {
            internal_id = internalId,
            platform_name = platformName,
            entity_type = entityType,
            platform_id = $"META_{metaKey}", // Use a placeholder or potentially the main platform ID if appropriate
            meta_key = metaKey,
            meta_value = metaValue,
            updated_at = DateTimeOffset.UtcNow
        };

        try
        {
            // Upsert based on internal_id, platform_name, entity_type, and meta_key
            var response = await _supabaseClient.From<PlatformIdMapping>(MappingsTable)
                .Upsert(mapping, new Postgrest.Models.UpsertOptions { OnConflict = "internal_id, platform_name, entity_type, meta_key" }, cancellationToken);

            response.ResponseMessage?.EnsureSuccessStatusCode();
            _logger.LogDebug("Saved meta mapping: InternalId {InternalId} - {Platform} {Type} {MetaKey} = {MetaValue}", internalId, platformName, entityType, metaKey, metaValue);
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error saving meta mapping {MetaKey} for InternalId {InternalId}, Platform {Platform}, Type {Type}", metaKey, internalId, platformName, entityType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error saving meta mapping {MetaKey} for InternalId {InternalId}, Platform {Platform}, Type {Type}", metaKey, internalId, platformName, entityType);
            throw;
        }
    }

     public async Task<Dictionary<string, string>> GetPlatformMappingsAsync(Guid internalId, string entityType, CancellationToken cancellationToken = default)
    {
        var mappings = new Dictionary<string, string>();
        try
        {
            var response = await _supabaseClient.From<PlatformIdMapping>(MappingsTable)
                .Select("platform_name, platform_id")
                .Filter("internal_id", Postgrest.Constants.Operator.Equals, internalId.ToString())
                .Filter("entity_type", Postgrest.Constants.Operator.Equals, entityType)
                .Filter("meta_key", Postgrest.Constants.Operator.IsNull, null) // Only main mappings
                .Get(cancellationToken);

            if (response.Models != null)
            {
                foreach (var model in response.Models)
                {
                    mappings[model.platform_name] = model.platform_id;
                }
            }
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error getting all mappings for InternalId {InternalId}, Type {Type}", internalId, entityType);
            // Don't throw, return potentially empty dictionary
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Generic error getting all mappings for InternalId {InternalId}, Type {Type}", internalId, entityType);
             // Don't throw
        }
        return mappings;
    }
} 
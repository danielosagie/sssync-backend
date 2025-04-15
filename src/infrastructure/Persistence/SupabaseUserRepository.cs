using Supabase;
using sssync_backend.core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Postgrest.Exceptions;

namespace sssync_backend.infrastructure.Persistence;

public class SupabaseUserRepository : SupabaseRepositoryBase, IUserRepository
{
     public SupabaseUserRepository(Client supabaseClient, ILogger<SupabaseUserRepository> logger)
        : base(supabaseClient, logger) { }

    public async Task<IEnumerable<string>> GetUserIdsWithActiveSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This query assumes you want distinct user_ids from the connections table.
            // Adjust if you have a separate 'users' table with an 'is_sync_enabled' flag.
            var response = await _supabaseClient.From<PlatformConnection>(ConnectionsTable)
                .Select("user_id")
                // Optional: Add filter for active connections if applicable
                // .Filter("is_active", Postgrest.Constants.Operator.Equals, true)
                .Get(cancellationToken);

            // Get distinct user IDs
            var userIds = response.Models?.Select(c => c.UserId).Distinct().ToList() ?? new List<string>();
            _logger.LogInformation("Found {Count} distinct users with platform connections.", userIds.Count);
            return userIds;
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error fetching user IDs with active sync");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error fetching user IDs with active sync");
            throw;
        }
    }
} 
using Supabase;
using sssync_backend.core.Interfaces.Repositories;
using sssync_backend.core.Models;
using Microsoft.Extensions.Logging;
using Postgrest.Exceptions; // For handling specific Supabase exceptions

namespace sssync_backend.infrastructure.Persistence;

public class SupabasePlatformConnectionRepository : SupabaseRepositoryBase, IPlatformConnectionRepository
{
    public SupabasePlatformConnectionRepository(Client supabaseClient, ILogger<SupabasePlatformConnectionRepository> logger)
        : base(supabaseClient, logger) { }

    public async Task<IEnumerable<PlatformConnection>> GetActiveConnectionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _supabaseClient.From<PlatformConnection>(ConnectionsTable) // Assuming PlatformConnection model matches table structure
                .Filter("user_id", Postgrest.Constants.Operator.Equals, userId)
                // Add filter for active status if you have one, e.g., .Filter("is_active", Postgrest.Constants.Operator.Equals, true)
                .Get(cancellationToken);

            // TODO: Decrypt AccessToken/RefreshToken here if encrypted in DB
            return response.Models ?? Enumerable.Empty<PlatformConnection>();
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error fetching active connections for user {UserId}", userId);
            throw; // Or handle more gracefully
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error fetching active connections for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PlatformConnection?> GetConnectionAsync(string userId, string platformName, CancellationToken cancellationToken = default)
    {
         try
        {
            var response = await _supabaseClient.From<PlatformConnection>(ConnectionsTable)
                .Filter("user_id", Postgrest.Constants.Operator.Equals, userId)
                .Filter("platform", Postgrest.Constants.Operator.Equals, platformName)
                .Single(cancellationToken); // Expects one or zero

            // TODO: Decrypt AccessToken/RefreshToken here if encrypted in DB
            return response;
        }
        catch (PostgrestException ex) when (ex.Message.Contains("0 rows")) // Handle case where no connection exists
        {
             _logger.LogInformation("No connection found for user {UserId} and platform {PlatformName}", userId, platformName);
             return null;
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error fetching connection for user {UserId}, platform {PlatformName}", userId, platformName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error fetching connection for user {UserId}, platform {PlatformName}", userId, platformName);
            throw;
        }
    }

    public async Task SaveConnectionAsync(PlatformConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        try
        {
            // TODO: Encrypt AccessToken/RefreshToken before saving if needed
            var connectionToSave = connection; // Create a DTO if encryption is needed

            // Upsert based on user_id and platform (assuming this is your unique constraint)
            var response = await _supabaseClient.From<PlatformConnection>(ConnectionsTable)
                .Upsert(connectionToSave, new Postgrest.Models.UpsertOptions { OnConflict = "user_id, platform" }, cancellationToken);

            response.ResponseMessage?.EnsureSuccessStatusCode(); // Throw if Supabase returned an error
             _logger.LogInformation("Successfully saved connection for user {UserId}, platform {Platform}", connection.UserId, connection.Platform);
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error saving connection for user {UserId}, platform {Platform}", connection.UserId, connection.Platform);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error saving connection for user {UserId}, platform {Platform}", connection.UserId, connection.Platform);
            throw;
        }
    }

     public async Task DeleteConnectionAsync(string userId, string platformName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _supabaseClient.From<PlatformConnection>(ConnectionsTable)
                 .Filter("user_id", Postgrest.Constants.Operator.Equals, userId)
                 .Filter("platform", Postgrest.Constants.Operator.Equals, platformName)
                 .Delete(cancellationToken: cancellationToken);

             _logger.LogInformation("Successfully deleted connection for user {UserId}, platform {PlatformName}", userId, platformName);
        }
        catch (PostgrestException ex)
        {
            _logger.LogError(ex, "Supabase error deleting connection for user {UserId}, platform {PlatformName}", userId, platformName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error deleting connection for user {UserId}, platform {PlatformName}", userId, platformName);
            throw;
        }
    }
} 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Postgrest.Exceptions;
using sssync_backend.core.Interfaces.Repositories;
using sssync_backend.core.Models;
using Supabase;
using Microsoft.Extensions.Logging;

namespace sssync_backend.infrastructure.Persistence.Repositories
{
    public class PlatformConnectionRepository : IPlatformConnectionRepository
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<PlatformConnectionRepository> _logger;
        private const string TableName = "PlatformConnections"; // Match your Supabase table name

        public PlatformConnectionRepository(Client supabaseClient, ILogger<PlatformConnectionRepository> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        public async Task<PlatformConnection?> GetByIdAsync(Guid connectionId, Guid userId)
        {
            try
            {
                var response = await _supabaseClient.From<PlatformConnection>(TableName)
                    .Select("*, Users!inner(*)") // Assuming FK relationship is set up or adjust selection
                    .Where(x => x.Id == connectionId && x.UserId == userId)
                    .Single();
                return response;
            }
            catch (PostgrestException ex) when (ex.Message.Contains("0 rows")) // Or check specific error code if available
            {
                _logger.LogInformation("PlatformConnection with Id {ConnectionId} for User {UserId} not found.", connectionId, userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PlatformConnection by Id {ConnectionId} for User {UserId}.", connectionId, userId);
                throw; // Re-throw unexpected exceptions
            }
        }

        public async Task<IEnumerable<PlatformConnection>> GetByUserIdAsync(Guid userId)
        {
            try
            {
                var response = await _supabaseClient.From<PlatformConnection>(TableName)
                    .Select("*")
                    .Where(x => x.UserId == userId)
                    .Get();
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PlatformConnections for User {UserId}.", userId);
                throw;
            }
        }

        public async Task<PlatformConnection?> GetByUserIdAndPlatformAsync(Guid userId, string platformType)
        {
             try
            {
                var response = await _supabaseClient.From<PlatformConnection>(TableName)
                    .Select("*")
                    .Where(x => x.UserId == userId && x.PlatformType == platformType)
                    .Limit(1)
                    .Single(); // Use Single to expect one or none
                return response;
            }
            catch (PostgrestException ex) when (ex.Message.Contains("0 rows"))
            {
                 _logger.LogInformation("PlatformConnection for User {UserId} and Platform {PlatformType} not found.", userId, platformType);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PlatformConnection for User {UserId} and Platform {PlatformType}.", userId, platformType);
                throw;
            }
        }

        public async Task<PlatformConnection> AddAsync(PlatformConnection connection)
        {
            if (connection.Id == Guid.Empty)
            {
                 connection.Id = Guid.NewGuid(); // Ensure ID is set if not provided
            }
            connection.CreatedAt = DateTimeOffset.UtcNow;
            connection.UpdatedAt = connection.CreatedAt;

            try
            {
                var response = await _supabaseClient.From<PlatformConnection>(TableName)
                    .Insert(connection);

                if (response.Models.Count > 0)
                {
                    return response.Models[0];
                }
                _logger.LogError("Failed to add PlatformConnection for User {UserId} and Platform {PlatformType}. Insert returned no models.", connection.UserId, connection.PlatformType);
                 throw new Exception("Failed to add platform connection.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding PlatformConnection for User {UserId} and Platform {PlatformType}.", connection.UserId, connection.PlatformType);
                throw;
            }
        }

        public async Task UpdateAsync(PlatformConnection connection)
        {
            connection.UpdatedAt = DateTimeOffset.UtcNow;
            try
            {
                // Use Upsert or Update depending on desired behavior and Supabase client version capabilities
                // Upsert might be safer if you're not sure the record exists
                await _supabaseClient.From<PlatformConnection>(TableName)
                             .Upsert(connection); // Or .Update(connection) if PK is correctly mapped
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error updating PlatformConnection {ConnectionId} for User {UserId}.", connection.Id, connection.UserId);
                 throw;
            }
        }

        public async Task DeleteAsync(Guid connectionId, Guid userId)
        {
             try
            {
                // Ensure deletion is scoped to the user for security
                await _supabaseClient.From<PlatformConnection>(TableName)
                    .Where(x => x.Id == connectionId && x.UserId == userId)
                    .Delete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting PlatformConnection {ConnectionId} for User {UserId}.", connectionId, userId);
                throw;
            }
        }
    }
} 
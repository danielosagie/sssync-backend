using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using sssync_backend.core.Models; // Assuming PlatformConnection is in this namespace

namespace sssync_backend.core.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for data access operations related to PlatformConnections.
    /// </summary>
    public interface IPlatformConnectionRepository
    {
        /// <summary>
        /// Retrieves a platform connection by its unique identifier.
        /// </summary>
        /// <param name="connectionId">The unique ID of the connection.</param>
        /// <param name="userId">The ID of the user owning the connection.</param>
        /// <returns>The PlatformConnection or null if not found or not owned by the user.</returns>
        Task<PlatformConnection?> GetByIdAsync(Guid connectionId, Guid userId);

        /// <summary>
        /// Retrieves all platform connections for a specific user.
        /// </summary>
        /// <param name="userId">The unique ID of the user.</param>
        /// <returns>A list of the user's platform connections.</returns>
        Task<IEnumerable<PlatformConnection>> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// Retrieves a specific platform connection for a user based on platform type.
        /// Assumes a user can only have one connection per platform type (adjust if needed).
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="platformType">The type of platform (e.g., "Shopify").</param>
        /// <returns>The specific PlatformConnection or null if not found.</returns>
        Task<PlatformConnection?> GetByUserIdAndPlatformAsync(Guid userId, string platformType);

        /// <summary>
        /// Adds a new platform connection to the data store.
        /// </summary>
        /// <param name="connection">The PlatformConnection object to add.</param>
        /// <returns>The added PlatformConnection, potentially with updated fields like Id or Timestamps.</returns>
        Task<PlatformConnection> AddAsync(PlatformConnection connection);

        /// <summary>
        /// Updates an existing platform connection in the data store.
        /// </summary>
        /// <param name="connection">The PlatformConnection object with updated values.</param>
        /// <returns>Task indicating completion.</returns>
        Task UpdateAsync(PlatformConnection connection);

        /// <summary>
        /// Deletes a platform connection from the data store.
        /// </summary>
        /// <param name="connectionId">The unique ID of the connection to delete.</param>
        /// <param name="userId">The ID of the user owning the connection.</param>
        /// <returns>Task indicating completion.</returns>
        Task DeleteAsync(Guid connectionId, Guid userId);
    } 
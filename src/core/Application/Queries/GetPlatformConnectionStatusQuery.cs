using System;

namespace sssync_backend.core.Application.Queries
{
    /// <summary>
    /// Query to retrieve the status of a specific platform connection.
    /// </summary>
    /// <param name="UserId">The ID of the user owning the connection.</param>
    /// <param name="ConnectionId">The ID of the specific connection.</param>
    public record GetPlatformConnectionStatusQuery(Guid UserId, Guid ConnectionId);

    // The result could be a simple string or a more detailed status DTO
    // public record PlatformConnectionStatusDto(Guid ConnectionId, string Status, DateTimeOffset? LastSyncSuccessAt);
    // public record GetPlatformConnectionStatusQueryResult(PlatformConnectionStatusDto Status);
} 
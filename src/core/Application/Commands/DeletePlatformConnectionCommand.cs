using System;
using MediatR;

namespace sssync_backend.core.Application.Commands
{
    /// <summary>
    /// Command to delete a specific platform connection.
    /// Uses IRequest (from MediatR) which returns Unit (void).
    /// </summary>
    /// <param name="UserId">The ID of the user owning the connection.</param>
    /// <param name="ConnectionId">The ID of the connection to delete.</param>
    public record DeletePlatformConnectionCommand(Guid UserId, Guid ConnectionId) : IRequest;

} 
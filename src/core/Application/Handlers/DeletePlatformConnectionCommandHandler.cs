using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using sssync_backend.core.Application.Commands;
using sssync_backend.core.Interfaces.Repositories;

namespace sssync_backend.core.Application.Handlers
{
    public class DeletePlatformConnectionCommandHandler : IRequestHandler<DeletePlatformConnectionCommand>
    {
        private readonly IPlatformConnectionRepository _connectionRepository;
        private readonly ILogger<DeletePlatformConnectionCommandHandler> _logger;

        public DeletePlatformConnectionCommandHandler(IPlatformConnectionRepository connectionRepository, ILogger<DeletePlatformConnectionCommandHandler> logger)
        {
            _connectionRepository = connectionRepository;
            _logger = logger;
        }

        public async Task<Unit> Handle(DeletePlatformConnectionCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to delete PlatformConnection {ConnectionId} for User {UserId}", request.ConnectionId, request.UserId);

            try
            {
                // Optional: Check if connection exists first? DeleteAsync should handle non-existence gracefully,
                // but you might want specific logging or behaviour.
                // var connection = await _connectionRepository.GetByIdAsync(request.ConnectionId, request.UserId);
                // if (connection == null)
                // {
                //     _logger.LogWarning("Attempted to delete non-existent or unauthorized PlatformConnection {ConnectionId} for User {UserId}", request.ConnectionId, request.UserId);
                //     // Return success anyway or throw NotFound depending on API contract
                //     return Unit.Value;
                // }

                await _connectionRepository.DeleteAsync(request.ConnectionId, request.UserId);

                _logger.LogInformation("Successfully deleted PlatformConnection {ConnectionId} for User {UserId}", request.ConnectionId, request.UserId);

                // TODO: Log activity in ActivityLogs table

                return Unit.Value; // Equivalent to void for MediatR
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting PlatformConnection {ConnectionId} for User {UserId}", request.ConnectionId, request.UserId);
                throw new ApplicationException("An error occurred while deleting the platform connection.", ex);
            }
        }
    }
} 
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using sssync_backend.core.Application.Queries;
using sssync_backend.core.Interfaces.Repositories;
using sssync_backend.core.Models;

namespace sssync_backend.core.Application.Handlers
{
    // Query Definition (in Queries folder)
    // public record GetPlatformConnectionStatusQuery(Guid UserId, Guid ConnectionId) : IRequest<PlatformConnection?>;

    public class GetPlatformConnectionStatusQueryHandler : IRequestHandler<GetPlatformConnectionStatusQuery, PlatformConnection?>
    {
        private readonly IPlatformConnectionRepository _connectionRepository;
        private readonly ILogger<GetPlatformConnectionStatusQueryHandler> _logger;

        public GetPlatformConnectionStatusQueryHandler(IPlatformConnectionRepository connectionRepository, ILogger<GetPlatformConnectionStatusQueryHandler> logger)
        {
            _connectionRepository = connectionRepository;
            _logger = logger;
        }

        public async Task<PlatformConnection?> Handle(GetPlatformConnectionStatusQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching status for PlatformConnection {ConnectionId} for User {UserId}", request.ConnectionId, request.UserId);
            try
            {
                // GetByIdAsync already ensures the connection belongs to the user
                var connection = await _connectionRepository.GetByIdAsync(request.ConnectionId, request.UserId);
                
                if (connection == null)
                {
                    _logger.LogWarning("PlatformConnection {ConnectionId} not found for User {UserId} when fetching status.", request.ConnectionId, request.UserId);
                    // Return null or throw a specific NotFound exception depending on API design
                }
                else
                {
                     _logger.LogInformation("Status for PlatformConnection {ConnectionId} is {Status}", connection.Id, connection.Status);
                }

                // Return the full connection or map to a status DTO
                return connection; 
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error fetching status for PlatformConnection {ConnectionId} for User {UserId}", request.ConnectionId, request.UserId);
                 throw new ApplicationException("An error occurred while fetching platform connection status.", ex);
            }
        }
    }
} 
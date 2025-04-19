using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using sssync_backend.core.Application.Queries; // Query definition
using sssync_backend.core.Interfaces.Repositories; // Repository Interface
using sssync_backend.core.Models; // PlatformConnection model

namespace sssync_backend.core.Application.Handlers
{
    // Query Definition (should be in Queries folder)
    // public record GetUserPlatformConnectionsQuery(Guid UserId) : IRequest<IEnumerable<PlatformConnection>>;

    public class GetUserPlatformConnectionsQueryHandler : IRequestHandler<GetUserPlatformConnectionsQuery, IEnumerable<PlatformConnection>>
    {
        private readonly IPlatformConnectionRepository _connectionRepository;
        private readonly ILogger<GetUserPlatformConnectionsQueryHandler> _logger;

        public GetUserPlatformConnectionsQueryHandler(IPlatformConnectionRepository connectionRepository, ILogger<GetUserPlatformConnectionsQueryHandler> logger)
        {
            _connectionRepository = connectionRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<PlatformConnection>> Handle(GetUserPlatformConnectionsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching platform connections for User {UserId}", request.UserId);

            try
            {
                var connections = await _connectionRepository.GetByUserIdAsync(request.UserId);
                _logger.LogInformation("Found {Count} connections for User {UserId}", connections.Count(), request.UserId);
                
                // Optional: Filter or map to a DTO if you don't want to return the full entity
                return connections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching platform connections for User {UserId}", request.UserId);
                // Depending on API design, you might return an empty list or throw
                throw new ApplicationException("An error occurred while fetching platform connections.", ex);
            }
        }
    }
} 
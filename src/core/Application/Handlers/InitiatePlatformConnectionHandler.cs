using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR; // Assuming MediatR is used
using Microsoft.Extensions.Logging;
using sssync_backend.core.Application.Commands; // Command definition
using sssync_backend.core.Interfaces.Platform; // IPlatformApiClient

namespace sssync_backend.core.Application.Handlers
{
    // Command definition (should be in Commands folder, assuming it exists)
    // public record InitiatePlatformConnectionCommand(Guid UserId, string PlatformType, string ShopName) : IRequest<string>; // Added ShopName!

    public class InitiatePlatformConnectionHandler : IRequestHandler<InitiatePlatformConnectionCommand, string>
    {
        private readonly IPlatformApiClientFactory _apiClientFactory; // Inject a factory
        private readonly ILogger<InitiatePlatformConnectionHandler> _logger;

        public InitiatePlatformConnectionHandler(IPlatformApiClientFactory apiClientFactory, ILogger<InitiatePlatformConnectionHandler> logger)
        {
            _apiClientFactory = apiClientFactory;
            _logger = logger;
        }

        public async Task<string> Handle(InitiatePlatformConnectionCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initiating platform connection for User {UserId}, Platform {PlatformType}, Shop {ShopName}", 
                request.UserId, request.PlatformType, request.ShopName);

            try
            {
                var apiClient = _apiClientFactory.GetClient(request.PlatformType);

                // Generate a unique state parameter for CSRF protection
                var state = Guid.NewGuid().ToString("N"); 
                
                // TODO: Temporarily store the state associated with the UserId and PlatformType (e.g., in cache or DB)
                // This is crucial for verifying the callback later.
                 _logger.LogWarning("State generation complete ({State}), but state persistence/verification is not implemented.", state);

                // Now we pass the required shopName to the API client
                var authorizationUrl = await apiClient.GetAuthorizationUrlAsync(state, request.ShopName);

                _logger.LogInformation("Generated authorization URL for User {UserId}, Platform {PlatformType}", 
                    request.UserId, request.PlatformType);

                return authorizationUrl;
            }
            catch (NotSupportedException nex)
            {
                 _logger.LogWarning(nex, "Platform type {PlatformType} is not supported.", request.PlatformType);
                 // Consider throwing a specific application exception or returning an error indicator
                 throw new ArgumentException($"Platform '{request.PlatformType}' is not supported.", nameof(request.PlatformType), nex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating platform connection for User {UserId}, Platform {PlatformType}",
                    request.UserId, request.PlatformType);
                // Consider throwing a specific application exception
                throw new ApplicationException("An error occurred while initiating the platform connection.", ex);
            }
        }
    }

    // --- Helper Factory Interface (Define this in Interfaces folder) ---
    // This allows injecting different clients based on platform type
    public interface IPlatformApiClientFactory
    {
        /// <summary>
        /// Gets the appropriate API client for the specified platform type.
        /// </summary>
        /// <param name="platformType">The platform type (e.g., "Shopify", "Clover").</param>
        /// <returns>An instance of IPlatformApiClient.</returns>
        /// <exception cref="NotSupportedException">Thrown if the platform type is not supported.</exception>
        IPlatformApiClient GetClient(string platformType);
    }
} 
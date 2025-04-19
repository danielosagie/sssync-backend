using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using sssync_backend.core.Application.Commands;
using sssync_backend.core.Interfaces.Platform;
using sssync_backend.core.Interfaces.Repositories;
using sssync_backend.core.Interfaces.Services;
using sssync_backend.core.Models;

namespace sssync_backend.core.Application.Handlers
{
    // Command definition (should be in Commands folder)
    // public record CompletePlatformConnectionCommand(
    //     Guid UserId, 
    //     string PlatformType, 
    //     string Code,         // Authorization code from callback
    //     string State,        // State from callback
    //     string ShopName      // Shop name from callback (e.g., Shopify)
    // ) : IRequest<PlatformConnection>; 

    public class CompletePlatformConnectionHandler : IRequestHandler<CompletePlatformConnectionCommand, PlatformConnection>
    {
        private readonly IPlatformApiClientFactory _apiClientFactory;
        private readonly IPlatformConnectionRepository _connectionRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<CompletePlatformConnectionHandler> _logger;

        public CompletePlatformConnectionHandler(
            IPlatformApiClientFactory apiClientFactory,
            IPlatformConnectionRepository connectionRepository,
            IEncryptionService encryptionService,
            ILogger<CompletePlatformConnectionHandler> logger)
        {
            _apiClientFactory = apiClientFactory;
            _connectionRepository = connectionRepository;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public async Task<PlatformConnection> Handle(CompletePlatformConnectionCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Completing platform connection for User {UserId}, Platform {PlatformType}, Shop {ShopName}",
                request.UserId, request.PlatformType, request.ShopName);

            // --- 1. State Verification (CRUCIAL) ---
            // TODO: Retrieve the expected state stored during the initiate step based on UserId/PlatformType
            // var expectedState = await _stateStore.GetAsync(request.UserId, request.PlatformType);
            string? expectedState = null; // Placeholder
            _logger.LogWarning("State verification is NOT IMPLEMENTED. This is a security risk (CSRF).");
            // if (expectedState == null || expectedState != request.State)
            // {
            //     _logger.LogError("Invalid state parameter received during OAuth callback for User {UserId}, Platform {PlatformType}. Expected '{ExpectedState}', Got '{ActualState}'.", 
            //         request.UserId, request.PlatformType, expectedState, request.State);
            //     throw new ApplicationException("Invalid state received during OAuth callback.");
            // }
            // TODO: Remove the state from the store after verification

            try
            {
                // --- 2. Exchange Code for Tokens ---
                var apiClient = _apiClientFactory.GetClient(request.PlatformType);
                var tokenResponse = await apiClient.ExchangeCodeForTokensAsync(request.Code, request.ShopName);

                _logger.LogInformation("Successfully exchanged code for tokens for User {UserId}, Platform {PlatformType}", request.UserId, request.PlatformType);

                // --- 3. Encrypt Credentials --- 
                // Serialize the relevant token info (e.g., access token, refresh token if present)
                // DO NOT store the raw code.
                var credentialsToStore = new Dictionary<string, string?>
                {
                    { "access_token", tokenResponse.AccessToken },
                    { "refresh_token", tokenResponse.RefreshToken }, // Will be null for Shopify typically
                    { "scope", tokenResponse.Scope }
                    // Add expiry info if available and needed
                };
                var credentialsJson = JsonSerializer.Serialize(credentialsToStore);
                var encryptedCredentials = await _encryptionService.EncryptAsync(credentialsJson);

                // --- 4. Check for Existing Connection --- 
                var existingConnection = await _connectionRepository.GetByUserIdAndPlatformAsync(request.UserId, request.PlatformType);

                PlatformConnection connectionToSave;
                if (existingConnection != null)
                {
                    _logger.LogInformation("Updating existing PlatformConnection {ConnectionId} for User {UserId}, Platform {PlatformType}", 
                        existingConnection.Id, request.UserId, request.PlatformType);
                    connectionToSave = existingConnection;
                    connectionToSave.EncryptedCredentials = encryptedCredentials;
                    connectionToSave.Status = "Connected"; // Update status
                    connectionToSave.DisplayName = request.ShopName; // Update display name (optional)
                    connectionToSave.IsEnabled = true; // Re-enable if previously disabled
                    connectionToSave.UpdatedAt = DateTimeOffset.UtcNow;
                    // Reset sync status if needed
                    connectionToSave.LastSyncAttemptAt = null;
                    connectionToSave.LastSyncSuccessAt = null;
                    
                    await _connectionRepository.UpdateAsync(connectionToSave);
                }
                else
                {
                     _logger.LogInformation("Creating new PlatformConnection for User {UserId}, Platform {PlatformType}", request.UserId, request.PlatformType);
                     connectionToSave = new PlatformConnection
                     {
                         UserId = request.UserId,
                         PlatformType = request.PlatformType,
                         DisplayName = request.ShopName, // Use shop name as initial display name
                         EncryptedCredentials = encryptedCredentials,
                         Status = "Connected",
                         IsEnabled = true,
                         CreatedAt = DateTimeOffset.UtcNow,
                         UpdatedAt = DateTimeOffset.UtcNow
                     };
                     connectionToSave = await _connectionRepository.AddAsync(connectionToSave);
                }
                
                _logger.LogInformation("Successfully saved PlatformConnection {ConnectionId} for User {UserId}, Platform {PlatformType}", 
                    connectionToSave.Id, request.UserId, request.PlatformType);

                // TODO: Log activity in ActivityLogs table

                return connectionToSave;
            }
            catch (Exception ex)
            {
                // TODO: Update connection status to "Error" or "NeedsReauth" if applicable
                _logger.LogError(ex, "Error completing platform connection for User {UserId}, Platform {PlatformType}", request.UserId, request.PlatformType);
                throw new ApplicationException("An error occurred while completing the platform connection.", ex);
            }
        }
    }
} 
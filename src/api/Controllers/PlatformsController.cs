using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using sssync_backend.core.Application.Commands;
using sssync_backend.core.Application.Queries;
using sssync_backend.core.Models; // For PlatformConnection return type

namespace sssync_backend.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all actions in this controller
    public class PlatformsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PlatformsController> _logger;

        public PlatformsController(IMediator mediator, ILogger<PlatformsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // Helper to get UserId from JWT claims
        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier); // Or Supabase specific claim
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            _logger.LogError("Could not parse User ID from claims. Claim value: {UserIdClaim}", userIdClaim);
            throw new UnauthorizedAccessException("User ID could not be determined from token.");
        }

        /// <summary>
        /// Initiates the OAuth connection flow for a specified platform.
        /// Requires the shop name (e.g., your-store.myshopify.com) for Shopify.
        /// </summary>
        /// <param name="platformType">The platform to connect to (e.g., "Shopify").</param>
        /// <param name="shopName">The shop identifier required by the platform (e.g., Shopify shop domain).</param>
        /// <returns>A redirect to the platform's authorization URL.</returns>
        [HttpGet("connect/initiate/{platformType}")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> InitiateConnection(string platformType, [FromQuery] string shopName)
        {
            if (string.IsNullOrWhiteSpace(shopName) && string.Equals(platformType, "Shopify", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Shopify connection initiation attempted without shopName query parameter.");
                return BadRequest("The 'shopName' query parameter is required for Shopify connections.");
            }

            var userId = GetUserId(); 
            _logger.LogInformation("InitiateConnection called for User {UserId}, Platform {PlatformType}, Shop {ShopName}", userId, platformType, shopName);

            var command = new InitiatePlatformConnectionCommand(userId, platformType, shopName);
            try
            {
                var authorizationUrl = await _mediator.Send(command);
                _logger.LogInformation("Redirecting User {UserId} to {AuthorizationUrl} for {PlatformType} auth", userId, authorizationUrl, platformType);
                return Redirect(authorizationUrl);
            }
            catch (ArgumentException argEx) when (argEx.ParamName == nameof(InitiatePlatformConnectionCommand.PlatformType))
            {
                _logger.LogWarning(argEx, "Unsupported platform type requested by User {UserId}: {PlatformType}", userId, platformType);
                 return BadRequest(argEx.Message); 
            }
            catch (ApplicationException appEx)
            {
                _logger.LogError(appEx, "Application error during InitiateConnection for User {UserId}, Platform {PlatformType}", userId, platformType);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while initiating the connection.");
            }
             catch (Exception ex)
            {
                 _logger.LogError(ex, "Unexpected error during InitiateConnection for User {UserId}, Platform {PlatformType}", userId, platformType);
                 return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Handles the OAuth callback from the external platform.
        /// </summary>
        /// <param name="platformType">The platform returning the callback.</param>
        /// <param name="code">The authorization code.</param>
        /// <param name="state">The state parameter for CSRF verification.</param>
        /// <param name="shop">The shop identifier (used by Shopify).</param>
        /// <returns>Success message or error.</returns>
        [HttpGet("connect/callback/{platformType}")]
        [AllowAnonymous] // Callback may not have Auth header initially, handle state verification carefully!
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlatformConnection))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ConnectionCallback(
            string platformType, 
            [FromQuery] string code,
            [FromQuery] string state,
            [FromQuery] string? shop = null) // Shopify includes shop, others might not
        {
             // IMPORTANT: This endpoint is AllowAnonymous because the redirect from the platform
            // won't have our JWT. Security relies *entirely* on:
            // 1. Verifying the 'state' parameter against a stored value associated with the user.
            // 2. The 'code' being single-use and validated by the platform.
            // We need the UserId associated with the state to proceed securely.

            _logger.LogInformation("ConnectionCallback received for Platform {PlatformType}, State {State}, Shop {Shop}", platformType, state, shop);

            // TODO: Retrieve UserId associated with the 'state' parameter from your temporary store.
            Guid? userId = null; // Placeholder
            _logger.LogWarning("User ID retrieval from state is NOT IMPLEMENTED. Cannot securely complete connection.");
            
            if (userId == null)
            {
                // This indicates an invalid/untrackable state or a failed lookup
                _logger.LogError("Could not associate callback state {State} with a User ID. Aborting connection.", state);
                return BadRequest("Invalid or expired callback state.");
            }

            // Use the shop parameter from the callback if available (especially for Shopify)
            var shopName = shop ?? ""; 
            if (string.IsNullOrWhiteSpace(shopName) && string.Equals(platformType, "Shopify", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Shopify callback received for User {UserId} without shop parameter.", userId.Value);
                return BadRequest("Required shop parameter missing in Shopify callback.");
            }

            var command = new CompletePlatformConnectionCommand(userId.Value, platformType, code, state, shopName);

            try
            {
                var connection = await _mediator.Send(command);
                _logger.LogInformation("Successfully completed connection {ConnectionId} for User {UserId}, Platform {PlatformType}", connection.Id, userId.Value, platformType);
                // Consider redirecting to a success page in the frontend application instead of returning JSON
                // return Redirect("https://your-frontend.app/connections?success=true");
                return Ok(connection); // Returning the connection details for now
            }
            catch (ApplicationException appEx) when (appEx.Message.Contains("Invalid state"))
            {
                 _logger.LogError(appEx, "Invalid state during ConnectionCallback for User {UserId}, Platform {PlatformType}", userId.Value, platformType);
                 return BadRequest(appEx.Message);
            }
             catch (Exception ex)
            {
                 _logger.LogError(ex, "Error during ConnectionCallback for User {UserId}, Platform {PlatformType}", userId.Value, platformType);
                 // Don't expose raw exception details
                 return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while completing the connection.");
            }
        }

        /// <summary>
        /// Gets all platform connections for the authenticated user.
        /// </summary>
        /// <returns>A list of platform connections.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<PlatformConnection>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetConnections()
        {
            var userId = GetUserId();
            var query = new GetUserPlatformConnectionsQuery(userId);
            var connections = await _mediator.Send(query);
            // Optional: Map to a smaller DTO if needed
            return Ok(connections);
        }

        /// <summary>
        /// Gets the status details of a specific platform connection.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <returns>The platform connection details or Not Found.</returns>
        [HttpGet("{connectionId}/status")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlatformConnection))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetConnectionStatus(Guid connectionId)
        {
             var userId = GetUserId();
             var query = new GetPlatformConnectionStatusQuery(userId, connectionId);
             var connection = await _mediator.Send(query);

             if (connection == null)
             {
                 return NotFound();
             }
             // Optional: Map to a smaller DTO
             return Ok(connection);
        }

        /// <summary>
        /// Deletes a specific platform connection for the authenticated user.
        /// </summary>
        /// <param name="connectionId">The ID of the connection to delete.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{connectionId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Optional: If you check existence first
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteConnection(Guid connectionId)
        {
            var userId = GetUserId();
            var command = new DeletePlatformConnectionCommand(userId, connectionId);
            
            try
            {
                 await _mediator.Send(command);
                 return NoContent(); 
            }
            catch (Exception ex) // Catch specific exceptions if needed (e.g., NotFound)
            {
                 _logger.LogError(ex, "Error deleting PlatformConnection {ConnectionId} for User {UserId}", connectionId, userId);
                 // Don't expose raw exception details
                 return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the connection.");
            }
        }
    }
} 
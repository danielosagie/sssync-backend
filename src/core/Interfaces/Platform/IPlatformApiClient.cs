using System.Threading.Tasks;

namespace sssync_backend.core.Interfaces.Platform
{
    /// <summary>
    /// Defines the contract for interacting with a specific platform's Authentication API (OAuth flow primarily).
    /// </summary>
    public interface IPlatformApiClient
    {
        /// <summary>
        /// Gets the platform-specific authorization URL to redirect the user to.
        /// </summary>
        /// <param name="state">A unique value to prevent CSRF attacks, included in the callback.</param>
        /// <param name="shopName">The specific shop identifier required by the platform (e.g., your-store.myshopify.com for Shopify).</param>
        /// <returns>The absolute URL for the authorization endpoint.</returns>
        Task<string> GetAuthorizationUrlAsync(string state, string shopName);

        /// <summary>
        /// Exchanges the authorization code received in the callback for access and refresh tokens.
        /// </summary>
        /// <param name="code">The authorization code provided by the platform.</param>
        /// <param name="shopName">The specific shop identifier the code belongs to (e.g., your-store.myshopify.com for Shopify).</param>
        /// <returns>A PlatformTokenResponse containing access token, refresh token (if applicable), expiry, and scope.</returns>
        /// <exception cref="System.Exception">Thrown if the token exchange fails.</exception>
        Task<PlatformTokenResponse> ExchangeCodeForTokensAsync(string code, string shopName);

        /// <summary>
        /// Uses a refresh token (if available) to obtain a new access token.
        /// </summary>
        /// <param name="refreshToken">The refresh token previously obtained.</param>
        /// <returns>A PlatformTokenResponse containing the new access token, potentially a new refresh token, expiry, and scope.</returns>
        /// <exception cref="System.Exception">Thrown if token refresh fails.</exception>
        Task<PlatformTokenResponse> RefreshAccessTokenAsync(string refreshToken);

        // Optional: Method to verify the connection is still valid using the access token
        // Task<bool> VerifyConnectionAsync(string accessToken);
    }

    /// <summary>
    /// Represents the response received from a platform after exchanging an auth code or refreshing a token.
    /// </summary>
    public class PlatformTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; } // Nullable as not all platforms/flows provide refresh tokens
        public int? ExpiresInSeconds { get; set; } // How long the access token is valid for (in seconds)
        public string Scope { get; set; } = string.Empty; // Permissions granted

        // Helper property to calculate approximate expiry time
        public DateTimeOffset? CalculatedExpiryUtc => ExpiresInSeconds.HasValue
            ? DateTimeOffset.UtcNow.AddSeconds(ExpiresInSeconds.Value - 60) // Subtract a buffer
            : null;
    }
} 
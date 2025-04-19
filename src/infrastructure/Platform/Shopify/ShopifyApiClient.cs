using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using sssync_backend.core.Interfaces.Platform;
using sssync_backend.infrastructure.Exceptions;

namespace sssync_backend.infrastructure.Platform.Shopify
{
    public class ShopifyApiClient : IPlatformApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ShopifyAppSettings _settings;
        private readonly ILogger<ShopifyApiClient> _logger;

        // Helper class to deserialize Shopify's token response
        private class ShopifyTokenResponseDto
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("scope")]
            public string Scope { get; set; } = string.Empty;

            // Note: Shopify's standard OAuth flow for public/custom apps often doesn't return
            // refresh tokens or expiry directly in the token exchange response.
            // Tokens are typically long-lived but associated with the install/user.
            // If using a different grant type or specific API version that does provide these,
            // add fields like expires_in and refresh_token here.
        }

        public ShopifyApiClient(IHttpClientFactory httpClientFactory, IOptions<ShopifyAppSettings> settings, ILogger<ShopifyApiClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ShopifyApiClient"); // Use a named client if needed
            _settings = settings.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.ApiSecret))
            {
                _logger.LogError("Shopify ApiKey or ApiSecret is not configured.");
                // Throwing here prevents using the client without proper setup
                throw new InvalidOperationException("Shopify API credentials missing in configuration.");
            }
        }

        public Task<string> GetAuthorizationUrlAsync(string state, string shopName)
        {
            if (string.IsNullOrWhiteSpace(shopName))
            {
                throw new ArgumentException("Shop name cannot be empty for Shopify authorization.", nameof(shopName));
            }

            // Ensure shopName doesn't include https:// or trailing slashes for URL building
            var sanitizedShopName = shopName.Replace("https://", "").Split('/')[0];

            var authUrl = $"https://{sanitizedShopName}/admin/oauth/authorize?"
                        + $"client_id={_settings.ApiKey}&"
                        + $"scope={_settings.RequiredScopes}&"
                        + $"redirect_uri={_settings.DefaultRedirectUrl}&"
                        + $"state={state}";
                        // Add &grant_options[]=per-user if needed for online access tokens
            _logger.LogDebug("Generated Shopify Auth URL: {AuthUrl}", authUrl);
            return Task.FromResult(authUrl);
        }

        public async Task<PlatformTokenResponse> ExchangeCodeForTokensAsync(string code, string shopName)
        {
             if (string.IsNullOrWhiteSpace(shopName))
            {
                throw new ArgumentException("Shop name cannot be empty for Shopify token exchange.", nameof(shopName));
            }
            var sanitizedShopName = shopName.Replace("https://", "").Split('/')[0];

            var tokenUrl = $"https://{sanitizedShopName}/admin/oauth/access_token";
            var requestBody = new Dictionary<string, string>
            {
                { "client_id", _settings.ApiKey },
                { "client_secret", _settings.ApiSecret },
                { "code", code }
            };

            _logger.LogInformation("Exchanging Shopify code for token at {TokenUrl} for shop {ShopName}", tokenUrl, sanitizedShopName);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
                {
                    Content = new FormUrlEncodedContent(requestBody)
                };

                var response = await _httpClient.SendAsync(request);

                var responseBody = await response.Content.ReadAsStringAsync(); // Read body regardless of status for logging

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Shopify token exchange failed with status {StatusCode}. Response: {ErrorContent}", response.StatusCode, responseBody);
                    throw new PlatformApiException($"Shopify token exchange failed: {response.ReasonPhrase}", responseBody);
                }

                _logger.LogDebug("Received Shopify token response: {ResponseBody}", responseBody);
                var shopifyResponse = JsonSerializer.Deserialize<ShopifyTokenResponseDto>(responseBody);

                if (shopifyResponse == null || string.IsNullOrWhiteSpace(shopifyResponse.AccessToken))
                {
                     _logger.LogError("Shopify token exchange response was successful but invalid (null or missing access_token). Body: {ResponseBody}", responseBody);
                     throw new PlatformApiException("Invalid token response from Shopify.", responseBody);
                }

                return new PlatformTokenResponse
                {
                    AccessToken = shopifyResponse.AccessToken,
                    Scope = shopifyResponse.Scope,
                    RefreshToken = null, // Typically not provided in standard flow
                    ExpiresInSeconds = null // Typically long-lived
                };
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error deserializing Shopify token response.");
                throw new PlatformApiException("Failed to parse Shopify token response.", jsonEx);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error during Shopify token exchange.");
                throw new PlatformApiException("Network error during Shopify token exchange.", httpEx);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Unexpected error during Shopify token exchange for shop {ShopName}.", sanitizedShopName);
                 throw;
            }
        }

        public Task<PlatformTokenResponse> RefreshAccessTokenAsync(string refreshToken)
        {
            // Standard Shopify OAuth for public/custom apps typically doesn't use refresh tokens.
            // Access tokens are generally long-lived and tied to the app installation.
            // If you have a specific scenario requiring refresh tokens, implement logic here.
            _logger.LogWarning("Shopify RefreshAccessTokenAsync called, but standard Shopify OAuth flow usually doesn't support refresh tokens.");
            throw new NotSupportedException("Shopify API client does not support token refresh via refresh token in the standard OAuth flow.");
        }
    }
} 
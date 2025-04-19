using System;
using Microsoft.Extensions.DependencyInjection;
using sssync_backend.core.Interfaces.Platform;
using sssync_backend.infrastructure.Platform.Shopify; // Need access to ShopifyApiClient

namespace sssync_backend.infrastructure.Platform
{
    /// <summary>
    /// Simple factory to provide platform-specific API client implementations.
    /// </summary>
    public class PlatformApiClientFactory : IPlatformApiClientFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public PlatformApiClientFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPlatformApiClient GetClient(string platformType)
        {
            // Case-insensitive comparison
            if (string.Equals(platformType, "Shopify", StringComparison.OrdinalIgnoreCase))
            {
                // Use service locator pattern here. Requires ShopifyApiClient to be registered.
                return _serviceProvider.GetRequiredService<ShopifyApiClient>();
            }
            // else if (string.Equals(platformType, "Clover", StringComparison.OrdinalIgnoreCase))
            // {
            //     return _serviceProvider.GetRequiredService<CloverApiClient>(); // When implemented
            // }
            else
            {
                throw new NotSupportedException($"Platform type '{platformType}' is not supported by the API client factory.");
            }
        }
    }
} 
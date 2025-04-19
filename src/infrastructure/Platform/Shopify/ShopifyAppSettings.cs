namespace sssync_backend.infrastructure.Platform.Shopify
{
    /// <summary>
    /// Configuration settings specific to the Shopify API integration.
    /// Maps to the "ShopifyApp" section in appsettings.json or User Secrets.
    /// </summary>
    public class ShopifyAppSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty; // Should come from User Secrets / Env Vars
        public string DefaultRedirectUrl { get; set; } = string.Empty; // Callback URL for OAuth
        public string RequiredScopes { get; set; } = "read_products,write_products,read_inventory,write_inventory,read_locations"; // Adjust scopes as needed
    }
} 
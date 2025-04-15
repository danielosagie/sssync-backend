using sssync_backend.core.Models;

namespace sssync_backend.core.Interfaces;

// Defines the contract for interacting with an external e-commerce platform
public interface IPlatformIntegrationService
{
    string PlatformName { get; } // e.g., "Shopify", "Clover", "Square"

    // --- Read Operations ---
    Task<IEnumerable<Location>> GetLocationsAsync(PlatformConnection connection);
    Task<IEnumerable<Product>> GetProductsAsync(PlatformConnection connection); // Should fetch variants and basic inventory too
    // Potentially add GetOrdersAsync, GetCustomersAsync etc. later

    // --- Write Operations ---
    Task<Product> CreateProductAsync(PlatformConnection connection, Product product);
    Task<Product> UpdateProductAsync(PlatformConnection connection, Product product);
    Task<bool> UpdateInventoryLevelAsync(PlatformConnection connection, Guid variantInternalId, Guid locationInternalId, int quantity);
    Task<Location> CreateLocationAsync(PlatformConnection connection, Location location); // Needed for mapping POS locations

    // --- Bulk Operations (Placeholders for future implementation) ---
    /// <summary>
    /// Exports products in a format suitable for bulk processing or backup.
    /// Consider using platform-specific bulk APIs (like Shopify's GraphQL Bulk Operations).
    /// </summary>
    /// <param name="connection">Platform connection details.</param>
    /// <returns>Path to the exported file or stream containing the data.</returns>
    Task<string> ExportProductsAsync(PlatformConnection connection);

    /// <summary>
    /// Imports products from a previously exported file or structured data.
    /// Consider using platform-specific bulk APIs.
    /// </summary>
    /// <param name="connection">Platform connection details.</param>
    /// <param name="importFilePath">Path to the file to import.</param>
    /// <returns>Status or ID of the bulk import job.</returns>
    Task<string> ImportProductsAsync(PlatformConnection connection, string importFilePath);

    // --- Helper/Mapping ---
    // May need methods to get platform-specific IDs based on internal IDs or vice-versa
    // These will require database lookups once repositories are implemented.
    Task<string?> GetPlatformProductIdAsync(PlatformConnection connection, Guid internalProductId);
    Task<string?> GetPlatformVariantIdAsync(PlatformConnection connection, Guid internalVariantId);
    Task<string?> GetPlatformLocationIdAsync(PlatformConnection connection, Guid internalLocationId);
    Task<string?> GetPlatformInventoryItemIdAsync(PlatformConnection connection, Guid internalVariantId); // Needed for inventory updates

}

// Represents the connection details for a specific platform for a user
// This would typically be stored securely in your database (Supabase)
public class PlatformConnection
{
    public string UserId { get; set; } = string.Empty; // Link to your user model
    public string Platform { get; set; } = string.Empty; // e.g., "Shopify"
    public string ShopDomain { get; set; } = string.Empty; // e.g., "my-store.myshopify.com" (for Shopify)
    public string AccessToken { get; set; } = string.Empty; // Encrypt this at rest!
    public DateTimeOffset? ExpiresAt { get; set; } // For OAuth tokens
    public string? RefreshToken { get; set; } // For OAuth tokens, encrypt this!
    // Add other platform-specific credentials as needed (API keys, secrets, etc.)
} 
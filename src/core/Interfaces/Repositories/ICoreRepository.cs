using sssync_backend.core.Models;

namespace sssync_backend.core.Interfaces.Repositories;

// Interface for managing the core sssync data models (Products, Variants, Locations, etc.)
public interface ICoreRepository
{
    // --- Product Methods ---
    Task<Product?> GetProductAsync(Guid internalProductId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductByPlatformIdAsync(string platformId, string platformName, CancellationToken cancellationToken = default); // Uses mapping internally
    Task<Product?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default); // Assumes SKU is unique enough for lookup
    Task SaveProductAsync(Product product, CancellationToken cancellationToken = default); // Creates or updates

    // --- Variant Methods ---
    Task<Variant?> GetVariantAsync(Guid internalVariantId, CancellationToken cancellationToken = default);
    Task<Variant?> GetVariantByPlatformIdAsync(string platformId, string platformName, CancellationToken cancellationToken = default); // Uses mapping internally
    Task<Variant?> GetVariantBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task SaveVariantAsync(Variant variant, CancellationToken cancellationToken = default); // Creates or updates

    // --- Location Methods ---
    Task<Location?> GetLocationAsync(Guid internalLocationId, CancellationToken cancellationToken = default);
    Task<Location?> GetLocationByPlatformIdAsync(string platformId, string platformName, CancellationToken cancellationToken = default); // Uses mapping internally
    Task SaveLocationAsync(Location location, CancellationToken cancellationToken = default); // Creates or updates
    Task<IEnumerable<Location>> GetLocationsByIdsAsync(IEnumerable<Guid> internalLocationIds, CancellationToken cancellationToken = default);


    // --- Inventory Level Methods ---
    Task<InventoryLevel?> GetInventoryLevelAsync(Guid internalVariantId, Guid internalLocationId, CancellationToken cancellationToken = default);
    Task SaveInventoryLevelAsync(InventoryLevel inventoryLevel, CancellationToken cancellationToken = default); // Creates or updates
    Task<IEnumerable<InventoryLevel>> GetInventoryLevelsForVariantAsync(Guid internalVariantId, CancellationToken cancellationToken = default);

    // --- Combined/Helper Methods ---
    /// <summary>
    /// Gets or creates a Product based on matching criteria (e.g., SKU, mapped Platform ID).
    /// Assigns InternalId if created. Returns the existing or newly created Product.
    /// </summary>
    Task<Product> GetOrCreateProductAsync(Product potentialProduct, string platformName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a Variant based on matching criteria (e.g., SKU, mapped Platform ID).
    /// Assigns InternalId and ProductInternalId if created. Returns the existing or newly created Variant.
    /// </summary>
    Task<Variant> GetOrCreateVariantAsync(Variant potentialVariant, Guid productInternalId, string platformName, CancellationToken cancellationToken = default);

     /// <summary>
    /// Gets or creates a Location based on matching criteria (e.g., Name, mapped Platform ID).
    /// Assigns InternalId if created. Returns the existing or newly created Location.
    /// </summary>
    Task<Location> GetOrCreateLocationAsync(Location potentialLocation, string platformName, CancellationToken cancellationToken = default);
} 
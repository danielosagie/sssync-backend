using Supabase;
using sssync_backend.core.Interfaces.Repositories;
using sssync_backend.core.Models;
using Microsoft.Extensions.Logging;
using Postgrest.Exceptions;

namespace sssync_backend.infrastructure.Persistence;

// NOTE: This implementation assumes your core Models (Product, Variant, Location, InventoryLevel)
// can be directly serialized/deserialized to/from your Supabase tables.
// If not, you'll need DTOs and mapping logic here.
// It also assumes simple primary key lookups and basic upserts.
// Complex queries or relationships might require more specific Postgrest syntax.

public class SupabaseCoreRepository : SupabaseRepositoryBase, ICoreRepository
{
    private readonly IMappingRepository _mappingRepository; // Needed for lookups by platform ID

    public SupabaseCoreRepository(Client supabaseClient, IMappingRepository mappingRepository, ILogger<SupabaseCoreRepository> logger)
        : base(supabaseClient, logger)
    {
        _mappingRepository = mappingRepository;
    }

    // --- Product Methods ---
    public async Task<Product?> GetProductAsync(Guid internalProductId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement fetching Product and its related Variants, InventoryLevels, etc.
        // This might require multiple queries or using Supabase RPC/Views if complex.
        _logger.LogWarning("GetProductAsync not fully implemented - requires fetching related data.");
        var response = await _supabaseClient.From<Product>(ProductsTable).Filter("internal_id", Postgrest.Constants.Operator.Equals, internalProductId).Single(cancellationToken);
        return response; // This only fetches the Product row itself
    }

    public async Task<Product?> GetProductByPlatformIdAsync(string platformId, string platformName, CancellationToken cancellationToken = default)
    {
        var internalId = await _mappingRepository.GetInternalIdAsync(platformId, platformName, "Product", cancellationToken);
        return internalId.HasValue ? await GetProductAsync(internalId.Value, cancellationToken) : null;
    }

     public async Task<Product?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        // This assumes SKU is unique across all products in your system.
        // It requires finding the variant with the SKU, then getting the product.
         _logger.LogWarning("GetProductBySkuAsync requires finding variant first, then product. Not fully implemented.");
        var variantResponse = await _supabaseClient.From<Variant>(VariantsTable).Filter("sku", Postgrest.Constants.Operator.Equals, sku).Limit(1).Get(cancellationToken);
        var variant = variantResponse.Models?.FirstOrDefault();
        return variant != null ? await GetProductAsync(variant.ProductInternalId, cancellationToken) : null;
    }


    public async Task SaveProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (product.InternalId == Guid.Empty) product.InternalId = Guid.NewGuid();
        product.UpdatedAt = DateTimeOffset.UtcNow;
        if (product.CreatedAt == DateTimeOffset.MinValue) product.CreatedAt = product.UpdatedAt;

        // TODO: Handle saving related Variants and their InventoryLevels.
        // This likely requires iterating through product.Variants and calling SaveVariantAsync.
        _logger.LogWarning("SaveProductAsync does not save related Variants/InventoryLevels yet.");

        var response = await _supabaseClient.From<Product>(ProductsTable)
            .Upsert(product, new Postgrest.Models.UpsertOptions { OnConflict = "internal_id" }, cancellationToken);
        response.ResponseMessage?.EnsureSuccessStatusCode();

        // Save mappings after product is saved
        foreach (var kvp in product.PlatformIds)
        {
            await _mappingRepository.SaveMappingAsync(product.InternalId, kvp.Key, "Product", kvp.Value, cancellationToken);
        }
    }

    // --- Variant Methods ---
    public async Task<Variant?> GetVariantAsync(Guid internalVariantId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement fetching Variant and its InventoryLevels.
        _logger.LogWarning("GetVariantAsync not fully implemented - requires fetching related data.");
        var response = await _supabaseClient.From<Variant>(VariantsTable).Filter("internal_id", Postgrest.Constants.Operator.Equals, internalVariantId).Single(cancellationToken);
        return response; // Only fetches Variant row
    }

     public async Task<Variant?> GetVariantByPlatformIdAsync(string platformId, string platformName, CancellationToken cancellationToken = default)
    {
        var internalId = await _mappingRepository.GetInternalIdAsync(platformId, platformName, "Variant", cancellationToken);
        return internalId.HasValue ? await GetVariantAsync(internalId.Value, cancellationToken) : null;
    }

    public async Task<Variant?> GetVariantBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var response = await _supabaseClient.From<Variant>(VariantsTable).Filter("sku", Postgrest.Constants.Operator.Equals, sku).Limit(1).Get(cancellationToken);
        // TODO: Fetch InventoryLevels for the variant
        return response.Models?.FirstOrDefault();
    }

    public async Task SaveVariantAsync(Variant variant, CancellationToken cancellationToken = default)
    {
         if (variant.InternalId == Guid.Empty) variant.InternalId = Guid.NewGuid();
         if (variant.ProductInternalId == Guid.Empty) throw new InvalidOperationException("Variant must have a ProductInternalId.");
         variant.UpdatedAt = DateTimeOffset.UtcNow;
         if (variant.CreatedAt == DateTimeOffset.MinValue) variant.CreatedAt = variant.UpdatedAt;

        // TODO: Handle saving InventoryLevels associated with this variant.
         _logger.LogWarning("SaveVariantAsync does not save related InventoryLevels yet.");

        var response = await _supabaseClient.From<Variant>(VariantsTable)
            .Upsert(variant, new Postgrest.Models.UpsertOptions { OnConflict = "internal_id" }, cancellationToken);
        response.ResponseMessage?.EnsureSuccessStatusCode();

        // Save mappings
        foreach (var kvp in variant.PlatformIds)
        {
            await _mappingRepository.SaveMappingAsync(variant.InternalId, kvp.Key, "Variant", kvp.Value, cancellationToken);
            // Potentially save InventoryItemId meta mapping here if available
        }
    }

    // --- Location Methods ---
     public async Task<Location?> GetLocationAsync(Guid internalLocationId, CancellationToken cancellationToken = default)
    {
        var response = await _supabaseClient.From<Location>(LocationsTable).Filter("internal_id", Postgrest.Constants.Operator.Equals, internalLocationId).Single(cancellationToken);
        return response;
    }

    public async Task<Location?> GetLocationByPlatformIdAsync(string platformId, string platformName, CancellationToken cancellationToken = default)
    {
        var internalId = await _mappingRepository.GetInternalIdAsync(platformId, platformName, "Location", cancellationToken);
        return internalId.HasValue ? await GetLocationAsync(internalId.Value, cancellationToken) : null;
    }

     public async Task SaveLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        if (location.InternalId == Guid.Empty) location.InternalId = Guid.NewGuid();
        location.UpdatedAt = DateTimeOffset.UtcNow;
        if (location.CreatedAt == DateTimeOffset.MinValue) location.CreatedAt = location.UpdatedAt;

        var response = await _supabaseClient.From<Location>(LocationsTable)
            .Upsert(location, new Postgrest.Models.UpsertOptions { OnConflict = "internal_id" }, cancellationToken);
        response.ResponseMessage?.EnsureSuccessStatusCode();

        // Save mappings
        foreach (var kvp in location.PlatformIds)
        {
            await _mappingRepository.SaveMappingAsync(location.InternalId, kvp.Key, "Location", kvp.Value, cancellationToken);
        }
    }

     public async Task<IEnumerable<Location>> GetLocationsByIdsAsync(IEnumerable<Guid> internalLocationIds, CancellationToken cancellationToken = default)
    {
        if (!internalLocationIds.Any()) return Enumerable.Empty<Location>();

        var response = await _supabaseClient.From<Location>(LocationsTable)
            .Filter("internal_id", Postgrest.Constants.Operator.In, internalLocationIds.Select(id => id.ToString()).ToList())
            .Get(cancellationToken);
        return response.Models ?? Enumerable.Empty<Location>();
    }


    // --- Inventory Level Methods ---
    public async Task<InventoryLevel?> GetInventoryLevelAsync(Guid internalVariantId, Guid internalLocationId, CancellationToken cancellationToken = default)
    {
        // Assumes composite key on variant_internal_id and location_internal_id
        var response = await _supabaseClient.From<InventoryLevel>(InventoryLevelsTable)
            .Filter("variant_internal_id", Postgrest.Constants.Operator.Equals, internalVariantId)
            .Filter("location_internal_id", Postgrest.Constants.Operator.Equals, internalLocationId)
            .Single(cancellationToken);
        return response;
    }

    public async Task SaveInventoryLevelAsync(InventoryLevel inventoryLevel, CancellationToken cancellationToken = default)
    {
        if (inventoryLevel.LocationInternalId == Guid.Empty || inventoryLevel.VariantInternalId == Guid.Empty) // Assuming VariantInternalId is added to the model or passed in
        {
             throw new InvalidOperationException("InventoryLevel must have both VariantInternalId and LocationInternalId.");
        }
        inventoryLevel.UpdatedAt = DateTimeOffset.UtcNow;

        // Need VariantInternalId on the InventoryLevel model for this to work easily
        // Add VariantInternalId to core/Models/InventoryLevel.cs
        // public Guid VariantInternalId { get; set; }

        var response = await _supabaseClient.From<InventoryLevel>(InventoryLevelsTable)
            .Upsert(inventoryLevel, new Postgrest.Models.UpsertOptions { OnConflict = "variant_internal_id, location_internal_id" }, cancellationToken);
        response.ResponseMessage?.EnsureSuccessStatusCode();
    }

     public async Task<IEnumerable<InventoryLevel>> GetInventoryLevelsForVariantAsync(Guid internalVariantId, CancellationToken cancellationToken = default)
    {
         var response = await _supabaseClient.From<InventoryLevel>(InventoryLevelsTable)
            .Filter("variant_internal_id", Postgrest.Constants.Operator.Equals, internalVariantId)
            .Get(cancellationToken);
        return response.Models ?? Enumerable.Empty<InventoryLevel>();
    }

    // --- Combined/Helper Methods ---
    public async Task<Product> GetOrCreateProductAsync(Product potentialProduct, string platformName, CancellationToken cancellationToken = default)
    {
        Product? existingProduct = null;
        string? platformProductId = potentialProduct.PlatformIds.GetValueOrDefault(platformName);

        // 1. Try lookup by platform mapping
        if (!string.IsNullOrEmpty(platformProductId))
        {
            existingProduct = await GetProductByPlatformIdAsync(platformProductId, platformName, cancellationToken);
        }

        // 2. Try lookup by SKU (if available and mapping didn't find it)
        // This assumes variants are processed first or SKU is on the product model
        // if (existingProduct == null && !string.IsNullOrEmpty(potentialProduct.Variants?.FirstOrDefault()?.Sku))
        // {
        //     existingProduct = await GetProductBySkuAsync(potentialProduct.Variants.First().Sku, cancellationToken);
        // }

        // 3. If still not found, create it
        if (existingProduct == null)
        {
            _logger.LogInformation("Creating new internal product for Title: {Title}, Platform: {Platform} ID: {PlatformId}", potentialProduct.Title, platformName, platformProductId ?? "N/A");
            potentialProduct.InternalId = Guid.NewGuid(); // Assign new internal ID
            // Ensure PlatformIds dictionary exists
            if (potentialProduct.PlatformIds == null) potentialProduct.PlatformIds = new Dictionary<string, string>();
            // Add the current platform ID if we have it
            if (!string.IsNullOrEmpty(platformProductId)) potentialProduct.PlatformIds[platformName] = platformProductId;

            await SaveProductAsync(potentialProduct, cancellationToken); // Save the new product (this will also save the mapping)
            return potentialProduct;
        }
        else
        {
             _logger.LogDebug("Found existing internal product {InternalId} for Platform: {Platform} ID: {PlatformId}", existingProduct.InternalId, platformName, platformProductId ?? "N/A");
            // Update existing product's mapping if needed
            if (!string.IsNullOrEmpty(platformProductId) && !existingProduct.PlatformIds.ContainsKey(platformName))
            {
                await _mappingRepository.SaveMappingAsync(existingProduct.InternalId, platformName, "Product", platformProductId, cancellationToken);
                existingProduct.PlatformIds[platformName] = platformProductId; // Update in-memory representation
            }
            // TODO: Add logic to merge/update fields if potentialProduct is newer?
            return existingProduct;
        }
    }

     public async Task<Variant> GetOrCreateVariantAsync(Variant potentialVariant, Guid productInternalId, string platformName, CancellationToken cancellationToken = default)
    {
        Variant? existingVariant = null;
        string? platformVariantId = potentialVariant.PlatformIds.GetValueOrDefault(platformName);

        // 1. Try lookup by platform mapping
        if (!string.IsNullOrEmpty(platformVariantId))
        {
            existingVariant = await GetVariantByPlatformIdAsync(platformVariantId, platformName, cancellationToken);
        }

        // 2. Try lookup by SKU (if available and mapping didn't find it)
        if (existingVariant == null && !string.IsNullOrEmpty(potentialVariant.Sku))
        {
            // Scope SKU lookup to the parent product if possible, otherwise global
            // This requires a more complex query or assumption of global SKU uniqueness
            existingVariant = await GetVariantBySkuAsync(potentialVariant.Sku, cancellationToken);
            // Ensure the found variant belongs to the correct product
            if (existingVariant != null && existingVariant.ProductInternalId != productInternalId)
            {
                 _logger.LogWarning("Found variant by SKU {Sku} but it belongs to different product {FoundProductId} instead of {ExpectedProductId}. Treating as new.", potentialVariant.Sku, existingVariant.ProductInternalId, productInternalId);
                 existingVariant = null;
            }
        }

        // 3. If still not found, create it
        if (existingVariant == null)
        {
            _logger.LogInformation("Creating new internal variant for SKU: {Sku}, Platform: {Platform} ID: {PlatformId}", potentialVariant.Sku ?? "N/A", platformName, platformVariantId ?? "N/A");
            potentialVariant.InternalId = Guid.NewGuid();
            potentialVariant.ProductInternalId = productInternalId;
            if (potentialVariant.PlatformIds == null) potentialVariant.PlatformIds = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(platformVariantId)) potentialVariant.PlatformIds[platformName] = platformVariantId;

            await SaveVariantAsync(potentialVariant, cancellationToken); // Saves variant and mapping
            return potentialVariant;
        }
        else
        {
             _logger.LogDebug("Found existing internal variant {InternalId} for Platform: {Platform} ID: {PlatformId}", existingVariant.InternalId, platformName, platformVariantId ?? "N/A");
            if (existingVariant.ProductInternalId == Guid.Empty) existingVariant.ProductInternalId = productInternalId; // Ensure parent link is set
            // Update mapping if needed
            if (!string.IsNullOrEmpty(platformVariantId) && !existingVariant.PlatformIds.ContainsKey(platformName))
            {
                await _mappingRepository.SaveMappingAsync(existingVariant.InternalId, platformName, "Variant", platformVariantId, cancellationToken);
                existingVariant.PlatformIds[platformName] = platformVariantId;
            }
            // TODO: Add logic to merge/update fields if potentialVariant is newer?
            return existingVariant;
        }
    }

     public async Task<Location> GetOrCreateLocationAsync(Location potentialLocation, string platformName, CancellationToken cancellationToken = default)
    {
        Location? existingLocation = null;
        string? platformLocationId = potentialLocation.PlatformIds.GetValueOrDefault(platformName);

        // 1. Try lookup by platform mapping
        if (!string.IsNullOrEmpty(platformLocationId))
        {
            existingLocation = await GetLocationByPlatformIdAsync(platformLocationId, platformName, cancellationToken);
        }

        // 2. Try lookup by Name (if mapping didn't find it - less reliable)
        // if (existingLocation == null && !string.IsNullOrEmpty(potentialLocation.Name))
        // {
        //     var response = await _supabaseClient.From<Location>(LocationsTable).Filter("name", Postgrest.Constants.Operator.Equals, potentialLocation.Name).Limit(1).Get(cancellationToken);
        //     existingLocation = response.Models?.FirstOrDefault();
        // }

        // 3. If still not found, create it
        if (existingLocation == null)
        {
            _logger.LogInformation("Creating new internal location for Name: {Name}, Platform: {Platform} ID: {PlatformId}", potentialLocation.Name, platformName, platformLocationId ?? "N/A");
            potentialLocation.InternalId = Guid.NewGuid();
            if (potentialLocation.PlatformIds == null) potentialLocation.PlatformIds = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(platformLocationId)) potentialLocation.PlatformIds[platformName] = platformLocationId;

            await SaveLocationAsync(potentialLocation, cancellationToken); // Saves location and mapping
            return potentialLocation;
        }
        else
        {
             _logger.LogDebug("Found existing internal location {InternalId} for Platform: {Platform} ID: {PlatformId}", existingLocation.InternalId, platformName, platformLocationId ?? "N/A");
            // Update mapping if needed
            if (!string.IsNullOrEmpty(platformLocationId) && !existingLocation.PlatformIds.ContainsKey(platformName))
            {
                await _mappingRepository.SaveMappingAsync(existingLocation.InternalId, platformName, "Location", platformLocationId, cancellationToken);
                existingLocation.PlatformIds[platformName] = platformLocationId;
            }
             // TODO: Add logic to merge/update fields if potentialLocation is newer?
            return existingLocation;
        }
    }
} 
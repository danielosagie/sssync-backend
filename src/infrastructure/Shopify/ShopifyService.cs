using Microsoft.Extensions.Logging;
using sssync_backend.core.Interfaces;
using sssync_backend.core.Models;
using ShopifySharp; // <-- Add this
using ShopifySharp.Filters; // <-- Add this for filters
using System.Linq; // <-- Add this for LINQ methods
using System.Collections.Generic; // <-- Add this for Dictionary/List
using sssync_backend.core.Interfaces.Repositories; // <-- Add Repository interface namespace
using ShopifySharp.GraphQL; // <-- Add for potential future GraphQL use

namespace sssync_backend.infrastructure.Shopify;

public class ShopifyService : IPlatformIntegrationService
{
    private readonly ILogger<ShopifyService> _logger;
    private readonly IMappingRepository _mappingRepository; // <-- Inject Mapping Repo

    // Inject repositories later for mapping lookups
    public ShopifyService(ILogger<ShopifyService> logger, IMappingRepository mappingRepository)
    {
        _logger = logger;
        _mappingRepository = mappingRepository; // <-- Assign repo
    }

    public string PlatformName => "Shopify";

    // --- Helper methods to create ShopifySharp service clients ---
    private LocationService GetLocationService(PlatformConnection connection) =>
       new LocationService(connection.ShopDomain, connection.AccessToken);
    private ProductService GetProductService(PlatformConnection connection) =>
       new ProductService(connection.ShopDomain, connection.AccessToken);
    private InventoryLevelService GetInventoryLevelService(PlatformConnection connection) =>
       new InventoryLevelService(connection.ShopDomain, connection.AccessToken);
    private InventoryItemService GetInventoryItemService(PlatformConnection connection) =>
       new InventoryItemService(connection.ShopDomain, connection.AccessToken);
    private ProductVariantService GetVariantService(PlatformConnection connection) =>
       new ProductVariantService(connection.ShopDomain, connection.AccessToken);


    public async Task<IEnumerable<Location>> GetLocationsAsync(PlatformConnection connection)
    {
        _logger.LogInformation("Fetching locations from Shopify for shop {ShopDomain}", connection.ShopDomain);
        var service = GetLocationService(connection);
        var coreLocations = new List<Location>();

        try
        {
            var shopifyLocations = await service.ListAsync(); // Shopify locations are usually few, pagination often not needed here

            foreach (var sl in shopifyLocations)
            {
                if (sl.Id == null) continue; // Skip if ID is missing

                coreLocations.Add(new Location
                {
                    // InternalId = Guid.NewGuid(), // Generate/Assign InternalId during the consolidation/saving phase in SyncService
                    Name = sl.Name ?? "Unnamed Location",
                    IsActive = sl.Active ?? false,
                    PlatformIds = new Dictionary<string, string> { { PlatformName, sl.Id.Value.ToString() } },
                    Address = MapShopifyAddressToCore(sl.Address),
                    CreatedAt = sl.CreatedAt ?? DateTimeOffset.MinValue,
                    UpdatedAt = sl.UpdatedAt ?? DateTimeOffset.MinValue
                });
            }
            _logger.LogInformation("Successfully fetched {Count} locations from Shopify for shop {ShopDomain}", coreLocations.Count, connection.ShopDomain);
        }
        catch (ShopifyException ex)
        {
            _logger.LogError(ex, "Shopify API error fetching locations for shop {ShopDomain}: {StatusCode} - {Error}", connection.ShopDomain, ex.HttpStatusCode, ex.Message);
            // Depending on the error (e.g., 401 Unauthorized), you might want to invalidate the connection token
            throw; // Re-throw or handle appropriately
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error fetching locations from Shopify for shop {ShopDomain}", connection.ShopDomain);
            throw; // Re-throw or handle appropriately
        }

        return coreLocations;
    }

    public async Task<IEnumerable<Product>> GetProductsAsync(PlatformConnection connection)
    {
        _logger.LogInformation("Fetching products from Shopify for shop {ShopDomain}", connection.ShopDomain);
        var productService = GetProductService(connection);
        var inventoryItemService = GetInventoryItemService(connection);
        var inventoryLevelService = GetInventoryLevelService(connection);
        var coreProducts = new List<Product>();

        // Fetch locations first to map inventory levels correctly
        // In a real scenario, locations might be cached or fetched once per sync cycle
        var locations = await GetLocationsAsync(connection);
        var locationPlatformIdMap = locations.ToDictionary(loc => loc.PlatformIds[PlatformName], loc => loc.InternalId); // Map Shopify Location ID -> Internal Guid (Requires InternalId assignment)
        // *** HACK: For now, since InternalId isn't assigned yet, we'll map Shopify ID -> Shopify ID for lookup within this method ***
        var tempLocationShopifyIdMap = locations.Where(l => l.PlatformIds.ContainsKey(PlatformName)).ToDictionary(l => l.PlatformIds[PlatformName], l => l.PlatformIds[PlatformName]);


        try
        {
            // Use pagination to fetch all products
            long? pageInfo = null;
            do
            {
                var filter = new ProductFilter { Limit = 250, PageInfo = pageInfo?.ToString() }; // Max limit is 250
                var productPage = await productService.ListAsync(filter);

                if (!productPage.Items.Any()) break;

                _logger.LogDebug("Fetched page with {Count} products from Shopify for shop {ShopDomain}", productPage.Items.Count(), connection.ShopDomain);

                // --- Prepare for Inventory Fetching ---
                // Get all variant IDs and their associated Shopify InventoryItem IDs from this page
                var variantInventoryItemIds = new Dictionary<long, long>(); // Shopify Variant ID -> Shopify Inventory Item ID
                foreach (var sp in productPage.Items)
                {
                    foreach (var sv in sp.Variants ?? Enumerable.Empty<ShopifySharp.ProductVariant>())
                    {
                        if (sv.Id.HasValue && sv.InventoryItemId.HasValue)
                        {
                            variantInventoryItemIds[sv.Id.Value] = sv.InventoryItemId.Value;
                        }
                    }
                }

                // Fetch Inventory Items (needed if tracking status changes, but often InventoryLevel is enough)
                // var inventoryItems = await inventoryItemService.ListAsync(new InventoryItemFilter { Ids = variantInventoryItemIds.Values });

                // Fetch Inventory Levels for all items on this page across known locations
                Dictionary<long, List<ShopifySharp.InventoryLevel>> inventoryLevelsByItemId = new();
                if (variantInventoryItemIds.Any() && tempLocationShopifyIdMap.Any())
                {
                    var inventoryItemIds = variantInventoryItemIds.Values.Distinct().ToList();
                    var locationIds = tempLocationShopifyIdMap.Keys.Select(long.Parse).ToList(); // Parse string IDs to long

                    // Fetch inventory levels in batches if necessary (Shopify might limit IDs per request)
                    const int batchSize = 50; // Adjust as needed
                    for (int i = 0; i < inventoryItemIds.Count; i += batchSize)
                    {
                        var itemBatch = inventoryItemIds.Skip(i).Take(batchSize).ToList();
                        var levelFilter = new InventoryLevelFilter
                        {
                            InventoryItemIds = itemBatch,
                            LocationIds = locationIds
                        };
                        var levelsPage = await inventoryLevelService.ListAsync(levelFilter); // This might also need pagination if many levels exist per item/location combo
                        foreach (var level in levelsPage.Items)
                        {
                            if (level.InventoryItemId.HasValue)
                            {
                                if (!inventoryLevelsByItemId.ContainsKey(level.InventoryItemId.Value))
                                {
                                    inventoryLevelsByItemId[level.InventoryItemId.Value] = new List<ShopifySharp.InventoryLevel>();
                                }
                                inventoryLevelsByItemId[level.InventoryItemId.Value].Add(level);
                            }
                        }
                    }
                }
                // --- End Inventory Fetching Prep ---


                // --- Map Products and Variants ---
                foreach (var sp in productPage.Items)
                {
                    if (sp.Id == null) continue;

                    var coreProduct = new Product
                    {
                        // InternalId = Guid.NewGuid(), // Assign during consolidation/saving
                        Title = sp.Title ?? string.Empty,
                        Description = sp.BodyHtml, // Or Body Plain? Choose based on needs
                        PlatformIds = new Dictionary<string, string> { { PlatformName, sp.Id.Value.ToString() } },
                        ImageUrls = sp.Images?.Select(img => img.Src).ToList() ?? new List<string>(),
                        CreatedAt = sp.CreatedAt ?? DateTimeOffset.MinValue,
                        UpdatedAt = sp.UpdatedAt ?? DateTimeOffset.MinValue,
                        Variants = new List<Variant>()
                        // Map other fields like Vendor, ProductType, Tags if needed
                    };

                    foreach (var sv in sp.Variants ?? Enumerable.Empty<ShopifySharp.ProductVariant>())
                    {
                        if (sv.Id == null || !sv.InventoryItemId.HasValue) continue;

                        var coreVariant = new Variant
                        {
                            // InternalId = Guid.NewGuid(), // Assign during consolidation/saving
                            // ProductInternalId = coreProduct.InternalId, // Assign during consolidation/saving
                            Sku = sv.SKU,
                            Barcode = sv.Barcode, // UPC/GTIN
                            Price = sv.Price ?? 0m,
                            CompareAtPrice = sv.CompareAtPrice,
                            Weight = sv.Grams, // Shopify uses grams, store consistently or add a Unit property
                            RequiresShipping = sv.RequiresShipping ?? false,
                            Taxable = sv.Taxable ?? false,
                            PlatformIds = new Dictionary<string, string> { { PlatformName, sv.Id.Value.ToString() } },
                            // Add variant image if sv.ImageId is present and matches an image in sp.Images
                            ImageUrls = sp.Images?.Where(img => img.Id == sv.ImageId).Select(img => img.Src).ToList() ?? new List<string>(),
                            CreatedAt = sv.CreatedAt ?? DateTimeOffset.MinValue,
                            UpdatedAt = sv.UpdatedAt ?? DateTimeOffset.MinValue,
                            InventoryLevels = new List<InventoryLevel>()
                            // Map Option1, Option2, Option3 if needed
                        };

                        // Map Inventory Levels for this variant
                        if (inventoryLevelsByItemId.TryGetValue(sv.InventoryItemId.Value, out var levels))
                        {
                            foreach (var level in levels)
                            {
                                if (level.LocationId.HasValue && level.Available.HasValue)
                                {
                                    // *** HACK: Using Shopify Location ID temporarily ***
                                    string shopifyLocationIdStr = level.LocationId.Value.ToString();
                                    if (tempLocationShopifyIdMap.ContainsKey(shopifyLocationIdStr))
                                    {
                                        coreVariant.InventoryLevels.Add(new InventoryLevel
                                        {
                                            // LocationInternalId = locationPlatformIdMap[shopifyLocationIdStr], // Use this once InternalIds are assigned
                                            LocationInternalId = Guid.Empty, // Placeholder - Needs real mapping
                                            AvailableQuantity = level.Available.Value,
                                            UpdatedAt = level.UpdatedAt ?? DateTimeOffset.MinValue,
                                            // PlatformIds could store the Shopify InventoryLevel ID if it exists/is useful
                                            PlatformIds = new Dictionary<string, string> {
                                                { "ShopifyLocationId", shopifyLocationIdStr }, // Store temp mapping info
                                                { "ShopifyInventoryItemId", sv.InventoryItemId.Value.ToString() }
                                            }
                                        });
                                    }
                                }
                            }
                        }
                        coreProduct.Variants.Add(coreVariant);
                    }
                    coreProducts.Add(coreProduct);
                }
                // --- End Mapping ---

                pageInfo = productPage.GetNextPageInfo(); // Prepare for the next page
                _logger.LogDebug("Moving to next page: {PageInfo}", pageInfo);

            } while (pageInfo != null);

            _logger.LogInformation("Successfully fetched {Count} total products from Shopify for shop {ShopDomain}", coreProducts.Count, connection.ShopDomain);
        }
        catch (ShopifyException ex)
        {
            _logger.LogError(ex, "Shopify API error fetching products for shop {ShopDomain}: {StatusCode} - {Error}", connection.ShopDomain, ex.HttpStatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error fetching products from Shopify for shop {ShopDomain}", connection.ShopDomain);
            throw;
        }

        return coreProducts;
    }

    // --- Write Operations (Stubs - Require DB/Mapping Implementation) ---

    public async Task<Product> CreateProductAsync(PlatformConnection connection, Product product)
    {
        _logger.LogInformation("Creating product '{ProductTitle}' on Shopify for shop {ShopDomain}", product.Title, connection.ShopDomain);
        var service = GetProductService(connection);

        try
        {
            // --- Map Core Product to Shopify Product ---
            var shopifyProduct = MapCoreProductToShopifyCreate(product);

            // Create the product on Shopify
            var createdShopifyProduct = await service.CreateAsync(shopifyProduct, new ProductCreateOptions { Published = true }); // Adjust options as needed

            if (createdShopifyProduct?.Id == null)
            {
                throw new Exception("Failed to create product on Shopify or returned product was invalid.");
            }

            _logger.LogInformation("Successfully created product on Shopify with ID {ShopifyProductId}", createdShopifyProduct.Id.Value);

            // --- Update Mappings ---
            string shopifyProductIdStr = createdShopifyProduct.Id.Value.ToString();
            await _mappingRepository.SaveMappingAsync(product.InternalId, PlatformName, "Product", shopifyProductIdStr);
            product.PlatformIds[PlatformName] = shopifyProductIdStr; // Update in-memory model

            // Map created variants and inventory item IDs
            if (createdShopifyProduct.Variants != null && product.Variants.Count == createdShopifyProduct.Variants.Count)
            {
                for (int i = 0; i < product.Variants.Count; i++)
                {
                    var coreVariant = product.Variants[i];
                    var shopifyVariant = createdShopifyProduct.Variants.ElementAt(i); // Assuming order is preserved

                    if (shopifyVariant.Id.HasValue)
                    {
                        string shopifyVariantIdStr = shopifyVariant.Id.Value.ToString();
                        await _mappingRepository.SaveMappingAsync(coreVariant.InternalId, PlatformName, "Variant", shopifyVariantIdStr);
                        coreVariant.PlatformIds[PlatformName] = shopifyVariantIdStr;

                        if (shopifyVariant.InventoryItemId.HasValue)
                        {
                            await _mappingRepository.SavePlatformMetaValueAsync(coreVariant.InternalId, PlatformName, "Variant", "InventoryItemId", shopifyVariant.InventoryItemId.Value.ToString());
                            // Update inventory levels if needed (might require separate calls after creation)
                            // await UpdateInventoryLevelsForNewVariant(connection, coreVariant, shopifyVariant.InventoryItemId.Value);
                        }
                    }
                }
            }
            else
            {
                 _logger.LogWarning("Mismatch between core variants and created Shopify variants for product {InternalId}", product.InternalId);
            }

            return product; // Return the updated core product model
        }
        catch (ShopifyException ex)
        {
            _logger.LogError(ex, "Shopify API error creating product '{ProductTitle}': {StatusCode} - {Error}", product.Title, ex.HttpStatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error creating product '{ProductTitle}' on Shopify", product.Title);
            throw;
        }
    }

    public async Task<Product> UpdateProductAsync(PlatformConnection connection, Product product)
    {
        _logger.LogInformation("Updating product '{ProductTitle}' (Internal ID: {InternalId}) on Shopify for shop {ShopDomain}", product.Title, product.InternalId, connection.ShopDomain);
        var productService = GetProductService(connection);
        var variantService = GetVariantService(connection); // For updating variants

        // --- Get Shopify Product ID ---
        string? shopifyProductIdStr = await _mappingRepository.GetPlatformIdAsync(product.InternalId, PlatformName, "Product");
        if (string.IsNullOrEmpty(shopifyProductIdStr) || !long.TryParse(shopifyProductIdStr, out long shopifyProductId))
        {
            _logger.LogError("Shopify Product ID not found or invalid for Internal Product ID {InternalId}. Cannot update.", product.InternalId);
            // Consider attempting to create it instead, or throw
            throw new InvalidOperationException($"Cannot update product {product.InternalId} on {PlatformName} without a valid mapped ID.");
        }

        try
        {
            // --- Map Core Product to Shopify Product for Update ---
            // Only include fields that need updating (Title, Description, Images, etc.)
            var shopifyProductUpdate = MapCoreProductToShopifyUpdate(product);

            // Update the main product details
            var updatedShopifyProduct = await productService.UpdateAsync(shopifyProductId, shopifyProductUpdate);
             _logger.LogDebug("Updated base product details for Shopify ID {ShopifyProductId}", shopifyProductId);

            // --- Update Variants ---
            foreach (var coreVariant in product.Variants)
            {
                string? shopifyVariantIdStr = await _mappingRepository.GetPlatformIdAsync(coreVariant.InternalId, PlatformName, "Variant");
                if (string.IsNullOrEmpty(shopifyVariantIdStr) || !long.TryParse(shopifyVariantIdStr, out long shopifyVariantId))
                {
                    _logger.LogWarning("Shopify Variant ID not found for Internal Variant ID {InternalVariantId}. Skipping update for this variant.", coreVariant.InternalId);
                    // TODO: Optionally attempt to create the variant if it's missing?
                    continue;
                }

                // Map core variant to Shopify variant for update (Price, SKU, Weight, Barcode, etc.)
                var shopifyVariantUpdate = MapCoreVariantToShopifyUpdate(coreVariant);

                try
                {
                    await variantService.UpdateAsync(shopifyVariantId, shopifyVariantUpdate);
                    _logger.LogDebug("Updated variant details for Shopify Variant ID {ShopifyVariantId}", shopifyVariantId);
                }
                catch (ShopifyException vex)
                {
                     _logger.LogError(vex, "Failed to update Shopify Variant ID {ShopifyVariantId} for Internal Variant {InternalVariantId}: {StatusCode} - {Error}", shopifyVariantId, coreVariant.InternalId, vex.HttpStatusCode, vex.Message);
                     // Continue with other variants?
                }
            }

            // --- Update Inventory (using separate method) ---
            // Inventory is usually updated via UpdateInventoryLevelAsync based on detected changes in SyncService

            // --- Update Images ---
            // Image updates are complex (matching, adding, removing). Often handled by updating the main product image list.
            // If specific image updates are needed, implement logic here using updatedShopifyProduct.Images.

            _logger.LogInformation("Successfully finished update process for product on Shopify with ID {ShopifyProductId}", shopifyProductId);
            // Return the original product model; SyncService handles the state.
            return product;
        }
        catch (ShopifyException ex)
        {
            _logger.LogError(ex, "Shopify API error updating product ID {ShopifyProductId}: {StatusCode} - {Error}", shopifyProductId, ex.HttpStatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error updating product ID {ShopifyProductId} on Shopify", shopifyProductId);
            throw;
        }
    }

     public async Task<bool> UpdateInventoryLevelAsync(PlatformConnection connection, Guid variantInternalId, Guid locationInternalId, int quantity)
    {
        _logger.LogInformation("Updating inventory for Variant {VariantInternalId} at Location {LocationInternalId} to quantity {Quantity} on Shopify for shop {ShopDomain}", variantInternalId, locationInternalId, quantity, connection.ShopDomain);

        // --- Get Platform IDs ---
        string? shopifyInventoryItemIdStr = await _mappingRepository.GetPlatformMetaValueAsync(variantInternalId, PlatformName, "Variant", "InventoryItemId");
        string? shopifyLocationIdStr = await _mappingRepository.GetPlatformIdAsync(locationInternalId, PlatformName, "Location");

        if (string.IsNullOrEmpty(shopifyInventoryItemIdStr) || !long.TryParse(shopifyInventoryItemIdStr, out long invItemId))
        {
            _logger.LogError("Could not find mapped Shopify InventoryItem ID for Variant {VariantInternalId}", variantInternalId);
            return false;
        }
        if (string.IsNullOrEmpty(shopifyLocationIdStr) || !long.TryParse(shopifyLocationIdStr, out long locId))
        {
            _logger.LogError("Could not find mapped Shopify Location ID for Location {LocationInternalId}", locationInternalId);
            return false;
        }

        var invLevelService = GetInventoryLevelService(connection);
        try
        {
            // Use SetAsync to set inventory to a specific quantity
            // Note: AdjustAsync is for relative changes (+/-)
            await invLevelService.SetAsync(new InventoryLevel { InventoryItemId = invItemId, LocationId = locId, Available = quantity });
            _logger.LogInformation("Successfully updated inventory on Shopify for Item {InvItemId} at Loc {LocId}.", invItemId, locId);
            return true;
        }
        catch (ShopifyException ex)
        {
            _logger.LogError(ex, "Failed to update inventory on Shopify for Item {InvItemId} at Loc {LocId}: {StatusCode} - {Error}", invItemId, locId, ex.HttpStatusCode, ex.Message);
            // Check for specific errors, e.g., 422 Unprocessable Entity might mean item not stocked at location
            return false;
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Generic error updating inventory on Shopify for Item {InvItemId} at Loc {LocId}", invItemId, locId);
             return false;
        }
    }

    public async Task<Location> CreateLocationAsync(PlatformConnection connection, Location location)
    {
        _logger.LogInformation("Creating location '{LocationName}' on Shopify for shop {ShopDomain}", location.Name, connection.ShopDomain);
        var service = GetLocationService(connection);

        try
        {
            var shopifyLocationToCreate = MapCoreLocationToShopify(location); // Create helper
            var createdShopifyLocation = await service.CreateAsync(shopifyLocationToCreate);

            if (createdShopifyLocation?.Id == null)
            {
                 throw new Exception("Failed to create location on Shopify or returned location was invalid.");
            }

             _logger.LogInformation("Successfully created location on Shopify with ID {ShopifyLocationId}", createdShopifyLocation.Id.Value);

            // --- Update Mapping ---
            string shopifyLocationIdStr = createdShopifyLocation.Id.Value.ToString();
            await _mappingRepository.SaveMappingAsync(location.InternalId, PlatformName, "Location", shopifyLocationIdStr);
            location.PlatformIds[PlatformName] = shopifyLocationIdStr;

            return location;
        }
        catch (ShopifyException ex)
        {
            _logger.LogError(ex, "Shopify API error creating location '{LocationName}': {StatusCode} - {Error}", location.Name, ex.HttpStatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generic error creating location '{LocationName}' on Shopify", location.Name);
            throw;
        }
    }

    // --- Bulk Operations (Stubs) ---
    public Task<string> ExportProductsAsync(PlatformConnection connection)
    {
        _logger.LogWarning("ExportProductsAsync: Not implemented. Recommend using Shopify GraphQL Bulk Operations API for efficiency.");
        // Implementation would involve:
        // 1. Constructing a GraphQL Bulk Operation query (mutation).
        // 2. Sending the mutation using a GraphQL client.
        // 3. Polling the operation status.
        // 4. Downloading the result file (JSONL) when complete.
        throw new NotImplementedException("Recommend using Shopify GraphQL Bulk Operations API.");
    }

    public Task<string> ImportProductsAsync(PlatformConnection connection, string importFilePath)
    {
        _logger.LogWarning("ImportProductsAsync: Not implemented. Recommend using Shopify GraphQL Bulk Operations API with mutation 'bulkOperationRunMutation' and staged uploads.");
        // Implementation would involve:
        // 1. Preparing the data in JSONL format.
        // 2. Generating staged upload URLs using GraphQL mutation 'stagedUploadsCreate'.
        // 3. Uploading the JSONL file to the URL.
        // 4. Starting the bulk import mutation ('bulkOperationRunMutation') using the staged upload key.
        // 5. Polling the operation status.
        throw new NotImplementedException("Recommend using Shopify GraphQL Bulk Operations API.");
    }


    // --- Helper/Mapping (Stubs - Require DB/Mapping Implementation) ---

    public async Task<string?> GetPlatformProductIdAsync(PlatformConnection connection, Guid internalProductId)
    {
        return await _mappingRepository.GetPlatformIdAsync(internalProductId, PlatformName, "Product");
    }

    public async Task<string?> GetPlatformVariantIdAsync(PlatformConnection connection, Guid internalVariantId)
    {
         return await _mappingRepository.GetPlatformIdAsync(internalVariantId, PlatformName, "Variant");
    }

     public async Task<string?> GetPlatformLocationIdAsync(PlatformConnection connection, Guid internalLocationId)
    {
         return await _mappingRepository.GetPlatformIdAsync(internalLocationId, PlatformName, "Location");
    }

    public async Task<string?> GetPlatformInventoryItemIdAsync(PlatformConnection connection, Guid internalVariantId)
    {
        // Retrieve the stored InventoryItemId from the mapping table
        return await _mappingRepository.GetPlatformMetaValueAsync(internalVariantId, PlatformName, "Variant", "InventoryItemId");
    }

    // --- Private Mapping Helpers ---
    private Address? MapShopifyAddressToCore(ShopifySharp.Address? shopifyAddress)
    {
        if (shopifyAddress == null) return null;
        return new Address
        {
            Address1 = shopifyAddress.Address1,
            Address2 = shopifyAddress.Address2,
            City = shopifyAddress.City,
            ProvinceCode = shopifyAddress.ProvinceCode,
            CountryCode = shopifyAddress.CountryCode,
            Zip = shopifyAddress.Zip,
            Phone = shopifyAddress.Phone
            // Map other fields if necessary (Company, Name, Latitude, Longitude)
        };
    }

    // Helper to map for creation
    private ShopifySharp.Product MapCoreProductToShopifyCreate(Product coreProduct)
    {
        return new ShopifySharp.Product
        {
            Title = coreProduct.Title,
            BodyHtml = coreProduct.Description,
            // PublishedAt = DateTimeOffset.UtcNow, // Publish immediately
            Images = coreProduct.ImageUrls?.Select(url => new ProductImage { Src = url }).ToList(),
            Variants = coreProduct.Variants?.Select(MapCoreVariantToShopifyCreate).ToList()
            // TODO: Map Vendor, ProductType, Tags if needed
        };
    }

    // Helper to map for update (only include fields you want to change)
    private ShopifySharp.Product MapCoreProductToShopifyUpdate(Product coreProduct)
    {
        return new ShopifySharp.Product
        {
            Id = long.Parse(coreProduct.PlatformIds[PlatformName]), // Required for update
            Title = coreProduct.Title,
            BodyHtml = coreProduct.Description,
            Images = coreProduct.ImageUrls?.Select(url => new ProductImage { Src = url }).ToList() // Updating images replaces the whole list
            // DO NOT include Variants here - update them separately
            // TODO: Map Vendor, ProductType, Tags if needed for update
        };
    }

     // Helper to map for variant creation
    private ShopifySharp.ProductVariant MapCoreVariantToShopifyCreate(Variant coreVariant)
    {
        return new ShopifySharp.ProductVariant
        {
            Price = coreVariant.Price,
            CompareAtPrice = coreVariant.CompareAtPrice,
            SKU = coreVariant.Sku,
            Barcode = coreVariant.Barcode,
            Grams = (long?)(coreVariant.Weight), // Convert double? to long?
            RequiresShipping = coreVariant.RequiresShipping,
            Taxable = coreVariant.Taxable,
            // InventoryPolicy = "deny", // Or "continue"
            // FulfillmentService = "manual", // Or your service handle
            // Option1, Option2, Option3 if used
            // ImageId - needs mapping if variant image exists
        };
    }

     // Helper to map for variant update
    private ShopifySharp.ProductVariant MapCoreVariantToShopifyUpdate(Variant coreVariant)
    {
        return new ShopifySharp.ProductVariant
        {
            Id = long.Parse(coreVariant.PlatformIds[PlatformName]), // Required for update
            Price = coreVariant.Price,
            CompareAtPrice = coreVariant.CompareAtPrice,
            SKU = coreVariant.Sku,
            Barcode = coreVariant.Barcode,
            Grams = (long?)(coreVariant.Weight),
            RequiresShipping = coreVariant.RequiresShipping,
            Taxable = coreVariant.Taxable,
            // Option1, Option2, Option3 if used
            // ImageId if changed
        };
    }

     // Helper to map core location to Shopify location for creation
    private ShopifySharp.Location MapCoreLocationToShopify(Location coreLocation)
    {
        return new ShopifySharp.Location
        {
            Name = coreLocation.Name,
            Address = MapCoreAddressToShopify(coreLocation.Address),
            Active = coreLocation.IsActive
        };
    }

    private ShopifySharp.Address MapCoreAddressToShopify(Address? coreAddress)
    {
        if (coreAddress == null) return new ShopifySharp.Address(); // Return empty or handle as needed
        return new ShopifySharp.Address
        {
            Address1 = coreAddress.Address1,
            Address2 = coreAddress.Address2,
            City = coreAddress.City,
            ProvinceCode = coreAddress.ProvinceCode,
            CountryCode = coreAddress.CountryCode,
            Zip = coreAddress.Zip,
            Phone = coreAddress.Phone
        };
    }
} 
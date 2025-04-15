using Microsoft.Extensions.Logging;
using sssync_backend.core.Interfaces;
using sssync_backend.core.Models;
using System.Collections.Generic;
using System.Linq;
using sssync_backend.core.Interfaces.Repositories;

namespace sssync_backend.core.Services;

public class SyncService : ISyncService
{
    // --- Dependencies ---
    // Inject repositories for fetching/saving connection details, product mappings, logs etc.
    private readonly ILogger<SyncService> _logger; // For logging
    private readonly IEnumerable<IPlatformIntegrationService> _platformServices; // For fetching data from platforms
    private readonly IPlatformConnectionRepository _connectionRepository; // For fetching active connections
    private readonly ICoreRepository _coreRepository; // For saving Products, Variants etc.
    private readonly IMappingRepository _mappingRepository; // For saving ID mappings

    public SyncService(
        ILogger<SyncService> logger,
        IEnumerable<IPlatformIntegrationService> platformServices,
        IPlatformConnectionRepository connectionRepository,
        ICoreRepository coreRepository,
        IMappingRepository mappingRepository)
    {
        _logger = logger;
        _platformServices = platformServices ?? throw new ArgumentNullException(nameof(platformServices));
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _coreRepository = coreRepository ?? throw new ArgumentNullException(nameof(coreRepository));
        _mappingRepository = mappingRepository ?? throw new ArgumentNullException(nameof(mappingRepository));
    }

    public async Task SynchronizeUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync for user {UserId}", userId);

        // --- Step 1: Fetch Connections ---
        var connections = await _connectionRepository.GetActiveConnectionsAsync(userId, cancellationToken);
        if (!connections.Any())
        {
            _logger.LogWarning("No active connections found for user {UserId}. Skipping sync.", userId);
            return;
        }
        _logger.LogInformation("Found {Count} active connections for user {UserId}.", connections.Count(), userId);


        // --- Step 2: Fetch Data From Each Platform ---
        var platformDataFetchTasks = connections.Select(conn => FetchDataFromPlatformAsync(conn, cancellationToken)).ToList();
        var fetchedPlatformDataResults = await Task.WhenAll(platformDataFetchTasks);
        var platformData = fetchedPlatformDataResults.Where(r => r != null).ToDictionary(r => r!.PlatformName, r => r!.Data); // Filter out nulls from failed fetches

        if (!platformData.Any())
        {
             _logger.LogWarning("No data successfully fetched from any platform for user {UserId}. Aborting sync.", userId);
             return;
        }

        // --- Step 3: Consolidate and Map Data ---
        _logger.LogInformation("Starting data consolidation for user {UserId}", userId);
        var consolidatedData = await ConsolidateAndMapDataAsync(platformData, cancellationToken);
        _logger.LogInformation("Finished data consolidation for user {UserId}. Processed {ProductCount} unique products.", userId, consolidatedData.Products.Count);


        // --- Step 4: Detect Changes and Conflicts ---
        _logger.LogInformation("Starting change detection for user {UserId}", userId);
        var changesToPush = DetectChanges(consolidatedData, platformData); // Implement this logic
        _logger.LogInformation("Finished change detection for user {UserId}. Found {ChangeCount} potential updates.", userId, changesToPush.Count);


        // --- Step 5: Push Updates to Platforms ---
        _logger.LogInformation("Starting update push for user {UserId}", userId);
        await PushUpdatesToPlatformsAsync(changesToPush, connections, cancellationToken);
        _logger.LogInformation("Finished update push for user {UserId}", userId);


        _logger.LogInformation("Sync completed successfully for user {UserId}", userId);
    }

    // Helper to fetch data for a single platform
    private async Task<PlatformFetchResult?> FetchDataFromPlatformAsync(PlatformConnection connection, CancellationToken cancellationToken)
    {
         var service = _platformServices.FirstOrDefault(s => s.PlatformName.Equals(connection.Platform, StringComparison.OrdinalIgnoreCase));
         if (service == null)
         {
             _logger.LogWarning("No integration service found for platform {Platform} for user {UserId}", connection.Platform, connection.UserId);
             return null;
         }

         try
         {
             _logger.LogInformation("Fetching data from {Platform} for user {UserId}", service.PlatformName, connection.UserId);
             var locationsTask = service.GetLocationsAsync(connection);
             var productsTask = service.GetProductsAsync(connection);
             await Task.WhenAll(locationsTask, productsTask);

             var locations = await locationsTask;
             var products = await productsTask;

             _logger.LogInformation("Fetched {LocationCount} locations and {ProductCount} products from {Platform} for user {UserId}",
                 locations.Count(), products.Count(), service.PlatformName, connection.UserId);

             return new PlatformFetchResult { PlatformName = service.PlatformName, Data = new PlatformData { Locations = locations, Products = products } };
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error fetching data from {Platform} for user {UserId}. Skipping platform.", service.PlatformName, connection.UserId);
             return null; // Return null on failure
         }
    }

     // Helper to consolidate data from all platforms into internal representation
    private async Task<ConsolidatedData> ConsolidateAndMapDataAsync(Dictionary<string, PlatformData> platformData, CancellationToken cancellationToken)
    {
        var consolidatedProducts = new Dictionary<Guid, Product>();
        var consolidatedLocations = new Dictionary<Guid, Location>();

        foreach (var kvp in platformData)
        {
            string platformName = kvp.Key;
            PlatformData data = kvp.Value;

            // Consolidate Locations
            foreach (var platLocation in data.Locations)
            {
                var coreLocation = await _coreRepository.GetOrCreateLocationAsync(platLocation, platformName, cancellationToken);
                if (!consolidatedLocations.ContainsKey(coreLocation.InternalId))
                {
                    consolidatedLocations.Add(coreLocation.InternalId, coreLocation);
                }
                // TODO: Merge properties if location already exists and platform data is newer?
            }

            // Consolidate Products and Variants
            foreach (var platProduct in data.Products)
            {
                var coreProduct = await _coreRepository.GetOrCreateProductAsync(platProduct, platformName, cancellationToken);
                if (!consolidatedProducts.ContainsKey(coreProduct.InternalId))
                {
                    // If new, initialize variants list before adding
                    coreProduct.Variants = new List<Variant>();
                    consolidatedProducts.Add(coreProduct.InternalId, coreProduct);
                }
                else
                {
                    // If exists, ensure variants list is initialized
                     coreProduct = consolidatedProducts[coreProduct.InternalId]; // Use the instance already in our dictionary
                     if (coreProduct.Variants == null) coreProduct.Variants = new List<Variant>();
                }
                 // TODO: Merge product properties if platform data is newer?

                foreach (var platVariant in platProduct.Variants)
                {
                    var coreVariant = await _coreRepository.GetOrCreateVariantAsync(platVariant, coreProduct.InternalId, platformName, cancellationToken);

                    // Store InventoryItemId mapping if present
                    if (platVariant.PlatformIds.TryGetValue($"{platformName}_InventoryItemId", out var invItemIdStr))
                    {
                        await _mappingRepository.SavePlatformMetaValueAsync(coreVariant.InternalId, platformName, "Variant", "InventoryItemId", invItemIdStr, cancellationToken);
                    }

                    // Find existing variant in consolidated list or add new
                    var existingConsolidatedVariant = coreProduct.Variants.FirstOrDefault(v => v.InternalId == coreVariant.InternalId);
                    if (existingConsolidatedVariant == null)
                    {
                        coreProduct.Variants.Add(coreVariant);
                        existingConsolidatedVariant = coreVariant; // Use the newly added one
                         if (existingConsolidatedVariant.InventoryLevels == null) existingConsolidatedVariant.InventoryLevels = new List<InventoryLevel>();
                    }
                    else
                    {
                         if (existingConsolidatedVariant.InventoryLevels == null) existingConsolidatedVariant.InventoryLevels = new List<InventoryLevel>();
                    }
                     // TODO: Merge variant properties if platform data is newer?


                    // Consolidate Inventory Levels
                    foreach (var platInvLevel in platVariant.InventoryLevels)
                    {
                        // Find the internal location ID using the temporary platform location ID
                        string? platLocationId = platInvLevel.PlatformIds.GetValueOrDefault($"{platformName}_LocationId");
                        if (platLocationId == null) continue;

                        var internalLocationId = await _mappingRepository.GetInternalIdAsync(platLocationId, platformName, "Location", cancellationToken);
                        if (!internalLocationId.HasValue)
                        {
                            _logger.LogWarning("Could not find internal location mapping for platform {Platform} location ID {PlatformLocationId}. Skipping inventory level.", platformName, platLocationId);
                            continue;
                        }

                        // Find existing inventory level or create new
                        var existingInvLevel = existingConsolidatedVariant.InventoryLevels.FirstOrDefault(il => il.LocationInternalId == internalLocationId.Value);
                        if (existingInvLevel == null)
                        {
                            platInvLevel.VariantInternalId = coreVariant.InternalId; // Set FK
                            platInvLevel.LocationInternalId = internalLocationId.Value;
                            await _coreRepository.SaveInventoryLevelAsync(platInvLevel, cancellationToken);
                            existingConsolidatedVariant.InventoryLevels.Add(platInvLevel);
                        }
                        else
                        {
                            // TODO: Update inventory level if platform data is newer?
                            if (platInvLevel.UpdatedAt > existingInvLevel.UpdatedAt)
                            {
                                existingInvLevel.AvailableQuantity = platInvLevel.AvailableQuantity;
                                existingInvLevel.UpdatedAt = platInvLevel.UpdatedAt;
                                await _coreRepository.SaveInventoryLevelAsync(existingInvLevel, cancellationToken);
                            }
                        }
                    }
                }
            }
        }

        // Fetch full consolidated data from DB? Or just use the in-memory build?
        // For simplicity, we'll use the in-memory build for now.
        return new ConsolidatedData
        {
            Products = consolidatedProducts.Values.ToList(),
            Locations = consolidatedLocations.Values.ToList()
        };
    }

    // Placeholder for change detection logic
    private List<PlatformUpdateAction> DetectChanges(ConsolidatedData consolidatedData, Dictionary<string, PlatformData> platformData)
    {
        var changes = new List<PlatformUpdateAction>();
        _logger.LogWarning("DetectChanges logic is not implemented. No updates will be pushed.");

        // --- Example Logic Outline ---
        // 1. Define Source of Truth (SoT): e.g., "Shopify" for inventory, maybe internal DB for Title/Description after first sync.
        // string inventorySoT = "Shopify";
        // string detailsSoT = "Internal"; // Or maybe last updated wins?

        // foreach (var coreProduct in consolidatedData.Products)
        // {
        //     foreach (var coreVariant in coreProduct.Variants)
        //     {
        //         // Get all platform representations of this variant
        //         var platformVariants = GetPlatformVariants(coreVariant.InternalId, platformData);

        //         // Compare Inventory
        //         foreach(var coreInvLevel in coreVariant.InventoryLevels)
        //         {
        //              var sotInvLevel = GetPlatformInventoryLevel(coreVariant.InternalId, coreInvLevel.LocationInternalId, inventorySoT, platformData);
        //              if (sotInvLevel == null) continue; // No source of truth data for this level

        //              foreach(var platformName in platformData.Keys.Where(p => p != inventorySoT))
        //              {
        //                   var targetInvLevel = GetPlatformInventoryLevel(coreVariant.InternalId, coreInvLevel.LocationInternalId, platformName, platformData);
        //                   if (targetInvLevel == null || targetInvLevel.AvailableQuantity != sotInvLevel.AvailableQuantity)
        //                   {
        //                        // Add change action to update inventory on 'platformName'
        //                        changes.Add(new PlatformUpdateAction { ... type = InventoryUpdate ... });
        //                   }
        //              }
        //         }

        //         // Compare Price, Title, SKU, etc. based on rules
        //         // ... add ProductUpdate actions ...
        //     }
        // }
        return changes;
    }

     // Placeholder for pushing updates
    private async Task PushUpdatesToPlatformsAsync(List<PlatformUpdateAction> changes, IEnumerable<PlatformConnection> connections, CancellationToken cancellationToken)
    {
        if (!changes.Any())
        {
            _logger.LogInformation("No changes detected to push.");
            return;
        }

        _logger.LogWarning("PushUpdatesToPlatformsAsync logic is not fully implemented.");

        foreach (var change in changes)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var service = _platformServices.FirstOrDefault(s => s.PlatformName.Equals(change.TargetPlatform, StringComparison.OrdinalIgnoreCase));
            var connection = connections.FirstOrDefault(c => c.Platform.Equals(change.TargetPlatform, StringComparison.OrdinalIgnoreCase));

            if (service == null || connection == null)
            {
                _logger.LogError("Cannot process update action: Service or Connection not found for platform {Platform}", change.TargetPlatform);
                continue;
            }

            try
            {
                switch (change.ActionType)
                {
                    case UpdateActionType.UpdateInventory:
                        _logger.LogInformation("Pushing Inventory Update: Variant {VariantId}, Location {LocationId}, Qty {Qty} to {Platform}",
                            change.VariantInternalId, change.LocationInternalId, change.Quantity, change.TargetPlatform);
                        await service.UpdateInventoryLevelAsync(connection, change.VariantInternalId, change.LocationInternalId, change.Quantity);
                        break;
                    case UpdateActionType.UpdateProduct:
                         _logger.LogInformation("Pushing Product Update: Product {ProductId} to {Platform}", change.ProductToUpdate.InternalId, change.TargetPlatform);
                         await service.UpdateProductAsync(connection, change.ProductToUpdate);
                         break;
                    case UpdateActionType.CreateProduct:
                         _logger.LogInformation("Pushing Create Product: Title '{Title}' to {Platform}", change.ProductToUpdate.Title, change.TargetPlatform);
                         await service.CreateProductAsync(connection, change.ProductToUpdate);
                         break;
                    // Add cases for CreateLocation, etc.
                    default:
                        _logger.LogWarning("Unsupported update action type: {ActionType}", change.ActionType);
                        break;
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Failed to push update action {ActionType} to {Platform} for Variant/Product {VariantOrProductId}",
                    change.ActionType, change.TargetPlatform, change.VariantInternalId != Guid.Empty ? change.VariantInternalId : change.ProductToUpdate?.InternalId);
                 // Decide whether to continue or stop on error
            }
        }
    }


    public async Task SynchronizeProductAsync(string userId, Guid internalProductId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting product sync for user {UserId}, product {InternalProductId}", userId, internalProductId);
        // TODO: Implement single product sync
        // 1. Fetch connections for user.
        // 2. Fetch product data *only* for internalProductId from DB (CoreRepository).
        // 3. Fetch product data *only* for the corresponding platform IDs from each platform (requires GetProductById on IPlatformIntegrationService or filtering).
        // 4. Compare DB version vs platform versions.
        // 5. Apply rules and push updates.
        _logger.LogWarning("SynchronizeProductAsync is not implemented.");
        await Task.CompletedTask; // Placeholder
    }

    // --- Helper Classes ---
    private class PlatformFetchResult {
        public required string PlatformName { get; set; }
        public required PlatformData Data { get; set; }
    }
    private class PlatformData
    {
        public IEnumerable<Location> Locations { get; set; } = Enumerable.Empty<Location>();
        public IEnumerable<Product> Products { get; set; } = Enumerable.Empty<Product>();
    }
    private class ConsolidatedData
    {
        public List<Product> Products { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
    }

    // Define structure for required updates
    private enum UpdateActionType { UpdateInventory, UpdateProduct, CreateProduct, CreateLocation /* etc. */ }
    private class PlatformUpdateAction
    {
        public UpdateActionType ActionType { get; set; }
        public string TargetPlatform { get; set; } = string.Empty;
        // Fields relevant to the action type
        public Guid VariantInternalId { get; set; }
        public Guid LocationInternalId { get; set; }
        public int Quantity { get; set; }
        public Product ProductToUpdate { get; set; } = null!; // For Product updates/creates
        // Add other fields as needed
    }
} 
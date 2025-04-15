using Supabase; // Assuming using supabase-csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; // For getting Supabase settings

namespace sssync_backend.infrastructure.Persistence;

/// <summary>
/// Base class for Supabase repositories providing the client instance.
/// </summary>
public abstract class SupabaseRepositoryBase
{
    protected readonly Client _supabaseClient;
    protected readonly ILogger _logger;

    // Table names (adjust to your actual schema)
    protected const string ConnectionsTable = "platform_connections";
    protected const string MappingsTable = "platform_id_mappings";
    protected const string ProductsTable = "products";
    protected const string VariantsTable = "variants";
    protected const string LocationsTable = "locations";
    protected const string InventoryLevelsTable = "inventory_levels";
    protected const string UsersTable = "users"; // Or Supabase auth users view/table

    protected SupabaseRepositoryBase(Client supabaseClient, ILogger logger)
    {
        _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Optional: Add common helper methods here if needed
}

// Define simple DTOs matching your Supabase table structure if they differ significantly
// from your core models or if you want finer control over serialization.
// Example:
// public class PlatformConnectionDto { /* ... properties matching table columns ... */ } 
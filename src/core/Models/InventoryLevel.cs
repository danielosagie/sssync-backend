namespace sssync_backend.core.Models;

// Represents the quantity of a variant at a specific location
public class InventoryLevel
{
    public Guid VariantInternalId { get; set; } // Foreign key to Variant (Added for DB relationship)
    public Guid LocationInternalId { get; set; } // Foreign key to Location
    public int AvailableQuantity { get; set; }
    public Dictionary<string, string> PlatformIds { get; set; } = new(); // Key: PlatformName, Value: Platform's InventoryLevel ID (if applicable)
    public DateTimeOffset UpdatedAt { get; set; }
} 
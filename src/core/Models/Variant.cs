namespace sssync_backend.core.Models;

// Represents a specific version of a product (e.g., size, color)
public class Variant
{
    public Guid InternalId { get; set; } // Unique ID within sssync
    public Guid ProductInternalId { get; set; } // Foreign key to Product
    public string? Sku { get; set; }
    public string? Barcode { get; set; } // UPC/GTIN
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public double? Weight { get; set; } // Consider units (e.g., grams, lbs) - maybe add a WeightUnit property
    public bool RequiresShipping { get; set; } = true;
    public bool Taxable { get; set; } = true;
    public List<InventoryLevel> InventoryLevels { get; set; } = new();
    public Dictionary<string, string> PlatformIds { get; set; } = new(); // Key: PlatformName, Value: Platform's Variant ID
    public List<string> ImageUrls { get; set; } = new(); // Variant-specific images
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    // Add Option1, Option2, Option3 for attributes like Size, Color, Material if needed
} 
namespace sssync_backend.core.Models;

// Represents a product in a platform-agnostic way
public class Product
{
    public Guid InternalId { get; set; } // Unique ID within sssync
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Variant> Variants { get; set; } = new();
    public List<string> ImageUrls { get; set; } = new();
    public Dictionary<string, string> PlatformIds { get; set; } = new(); // Key: PlatformName (e.g., "Shopify"), Value: Platform's Product ID
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    // Add other common fields like Vendor, ProductType, Tags etc. as needed
} 
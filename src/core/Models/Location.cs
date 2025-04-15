namespace sssync_backend.core.Models;

// Represents a physical or logical location where inventory is stored
public class Location
{
    public Guid InternalId { get; set; } // Unique ID within sssync
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Address? Address { get; set; }
    public Dictionary<string, string> PlatformIds { get; set; } = new(); // Key: PlatformName, Value: Platform's Location ID
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
} 
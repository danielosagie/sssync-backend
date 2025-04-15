namespace sssync_backend.core.Models;

// Simple address model, expand as needed
public class Address
{
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? ProvinceCode { get; set; } // State/Province abbreviation
    public string? CountryCode { get; set; } // ISO country code
    public string? Zip { get; set; }
    public string? Phone { get; set; }
} 
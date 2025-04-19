using System;

namespace sssync_backend.core.Models
{
    /// <summary>
    /// Represents a connection established by a user to an external platform.
    /// Maps closely to the PlatformConnections table in the database.
    /// </summary>
    public class PlatformConnection
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; } // FK to Users table / Supabase Auth User
        public string PlatformType { get; set; } = string.Empty; // e.g., "Shopify", "Clover"
        public string DisplayName { get; set; } = string.Empty; // User-friendly name or Shop name
        public string EncryptedCredentials { get; set; } = string.Empty; // Stores the JSON credentials AFTER encryption
        public string Status { get; set; } = string.Empty; // e.g., "Connected", "NeedsReauth", "Error"
        public bool IsEnabled { get; set; } = true;
        public DateTimeOffset? LastSyncAttemptAt { get; set; }
        public DateTimeOffset? LastSyncSuccessAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // Consider adding helper methods or properties if needed, e.g.,
        // to deserialize credentials (though decryption should happen elsewhere).
    }
} 
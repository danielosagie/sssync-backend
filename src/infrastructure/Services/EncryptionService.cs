using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using sssync_backend.core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System;

namespace sssync_backend.infrastructure.Services
{
    /// <summary>
    /// Implements encryption and decryption using ASP.NET Core Data Protection APIs.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly IDataProtector _protector;
        private readonly ILogger<EncryptionService> _logger;

        // Inject IDataProtectionProvider and create a protector with a specific purpose string
        public EncryptionService(IDataProtectionProvider provider, ILogger<EncryptionService> logger)
        {
            // The purpose string isolates protectors. Change "sssync_credentials" if needed,
            // but ensure it's consistent. If you change it, previously encrypted data won't decrypt.
            _protector = provider.CreateProtector("sssync_credentials");
            _logger = logger;
        }

        public Task<string> EncryptAsync(string plainText)
        {
            try
            {
                var encryptedText = _protector.Protect(plainText);
                return Task.FromResult(encryptedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting data.");
                throw; // Re-throw to indicate failure
            }
        }

        public Task<string> DecryptAsync(string encryptedText)
        {
            try
            {
                var plainText = _protector.Unprotect(encryptedText);
                return Task.FromResult(plainText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting data. Data might be corrupted or the protection key changed.");
                // Re-throw as CryptographicException to match interface (though original might differ)
                throw new System.Security.Cryptography.CryptographicException("Failed to decrypt data.", ex);
            }
        }
    }
} 
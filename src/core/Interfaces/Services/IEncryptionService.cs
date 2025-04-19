using System.Threading.Tasks;

namespace sssync_backend.core.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for encrypting and decrypting sensitive data, such as OAuth tokens.
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypts the provided plaintext data.
        /// </summary>
        /// <param name="plainText">The data to encrypt.</param>
        /// <returns>An encrypted string representation of the data.</returns>
        Task<string> EncryptAsync(string plainText);

        /// <summary>
        /// Decrypts the provided encrypted data.
        /// </summary>
        /// <param name="encryptedText">The data to decrypt.</param>
        /// <returns>The original plaintext data.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown if decryption fails.</exception>
        Task<string> DecryptAsync(string encryptedText);
    }
} 
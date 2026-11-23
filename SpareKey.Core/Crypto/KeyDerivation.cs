using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace SpareKey.Core.Crypto
{
    /// <summary>
    /// Turns a secret, password or the 24 words
    /// into a 32 byte key with Argon2id.
    ///
    /// secret + salt + settings  | Argon2id |  32byte box key
    /// </summary>
    internal static class KeyDerivation
    {
        public const int SaltSize = 16;   // 16 random bytes
        public const int KeySize = 32;    // matches AesGcmHelper.KeySize

        /// <summary>
        /// Makes a new random salt. Its not secret
        /// </summary>
        public static byte[] CreateSalt()
        {
            return RandomNumberGenerator.GetBytes(SaltSize);
        }

        /// <summary>
        /// For Box 1. The password the user types.
        /// </summary>
        public static byte[] DeriveKeyFromPassword(string password, byte[] salt, Argon2Settings settings)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password can't be empty.", nameof(password));
            }

            // Some letters like "é" can be stored as one character, or as "e" plus
            // a separate accent mark. They look identical but are different bytes.
            // We normalise them so the password works everywhere.
            string normalizedPassword = password.Normalize(NormalizationForm.FormC);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(normalizedPassword);

            try
            {
                return DeriveKeyFromBytes(passwordBytes, salt, settings);
            }
            finally
            {
                // Wipe our copy of the password bytes as soon as we r done
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }

        /// <summary>
        /// For Box 2: the 32 bytes that come from the 24 words.
        /// Also used by DeriveKeyFromPassword after it turns the text into bytes.
        ///
        /// This is slow, better call it from a UI thread
        /// </summary>
        public static byte[] DeriveKeyFromBytes(byte[] secret, byte[] salt, Argon2Settings settings)
        {
            if (secret == null || secret.Length == 0)
            {
                throw new ArgumentException("Secret can't be empty.", nameof(secret));
            }
            if (salt == null || salt.Length < SaltSize)
            {
                throw new ArgumentException("Salt must be at least " + SaltSize + " bytes.", nameof(salt));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Validate();

            using (Argon2id argon2 = new Argon2id(secret))
            {
                argon2.Salt = salt;
                argon2.MemorySize = settings.MemoryKiB; 
                argon2.Iterations = settings.Iterations;
                argon2.DegreeOfParallelism = settings.Parallelism;

                return argon2.GetBytes(KeySize);
            }
        }
    }
}
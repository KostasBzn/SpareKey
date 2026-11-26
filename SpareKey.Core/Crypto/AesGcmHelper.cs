using System.Security.Cryptography;

namespace SpareKey.Core.Crypto
{
    /// <summary>
    /// Locks and unlocks bytes with AES256-GCM.
    /// This class knows nothing about passwords, salts or files.
    /// It only takes a readymade 32-byte key.
    /// </summary>
    internal static class AesGcmHelper
    {
        /* FORMAT (Basically the format of the Encryption result)
         * --------------------------------------------------
         * | NONCE |        CIPHERTEXT        |     TAG     |
         * --------------------------------------------------
         *  12 bytes   same length as plaintext    16 bytes
         *
         * NONCE: random, new on every Encrypt call. Not secret.
         * CIPHERTEXT: the locked data.
         * TAG: a seal. If even one byte changes anywhere, Decrypt fails.
         */

        public const int KeySize = 32;     // 32 bytes means 256bit key (we encrypt with Aes256)
        public const int NonceSize = 12;   // the standard size for GCM

        // tag is calculated from key + ciphertext + asiciated data. stored next oto the ciphertext
        // this is used in decrypt and ensures nothing was changed / tampered
        public const int TagSize = 16;     
        public const int Overhead = NonceSize + TagSize;

        /// <summary>
        /// Encrypts the plaintext and returns NONCE + CIPHERTEXT + TAG in one array.
        /// associatedData can be null if there is nothing extra.
        /// </summary>
        public static byte[] Encrypt(byte[] key, byte[] plaintext, byte[] associatedData)
        {
            CheckKey(key);
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            // always new random nonce every single time
            // Reusing it can put us in deep shit. Why?
            // it can leak the data by letting an attacker forge tags.
            // Why we make it here? Because as a parameter makes the encryption more vulnerable
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];

            using (AesGcm aes = new AesGcm(key, TagSize))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            }

            // Glue all the stuff in one array
            byte[] result = new byte[NonceSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

            return result;
        }

        /// <summary>
        /// Takes NONCE + CIPHERTEXT + TAG and returns the plaintext.
        /// Throws a CryptographicException if the key is wrong or anything was changed.
        /// </summary>
        public static byte[] Decrypt(byte[] key, byte[] encryptedData, byte[] associatedData)
        {
            CheckKey(key);
            if (encryptedData == null)
            {
                throw new ArgumentNullException(nameof(encryptedData));
            }
            if (encryptedData.Length < Overhead)
            {
                throw new CryptographicException("Encrypted data is too short to be valid.");
            }

            int ciphertextLength = encryptedData.Length - Overhead;

            // Split the array back into its three parts
            byte[] nonce = new byte[NonceSize];
            byte[] ciphertext = new byte[ciphertextLength];
            byte[] tag = new byte[TagSize];
            Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(encryptedData, NonceSize, ciphertext, 0, ciphertextLength);
            Buffer.BlockCopy(encryptedData, NonceSize + ciphertextLength, tag, 0, TagSize);

            byte[] plaintext = new byte[ciphertextLength];

            using (AesGcm aes = new AesGcm(key, TagSize))
            {
                // This is where the tag gets checked
                // data or one flipped byte throws AuthenticationTagMismatchException
                aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            }

            return plaintext;
        }

        private static void CheckKey(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (key.Length != KeySize)
            {
                // AesGcm would also accept 16 or 24 byte keys 
                // We only want AES256, so anything else is a bug.
                throw new ArgumentException("Key must be exactly " + KeySize + " bytes.", nameof(key));
            }
        }
    }
}
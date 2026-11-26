using System.Security.Cryptography;

namespace SpareKey.Core.Crypto
{
    /// <summary>
    /// One safe "boxes" that keeps the master key
    ///
    ///   Box 1 is locked with your password.
    ///   Box 2 is locked with the bytes from the 24 words.
    /// </summary>
    internal class MasterKeyBox
    {
        /* WHAT A BOX HOLDS
         * -------------------------------------------------------------
         * | KDF TYPE | SETTINGS | SALT |   ENCRYPTED MASTER KEY      |
         * -------------------------------------------------------------
         *   1 = Argon2id           16 b   nonce 12 + key 32 + tag 16
         *                                 = 60 bytes
         */

        // Only one KDF for now. The number is stored in the file so a
        // future version could add another type and still be able to open older files
        public const int KdfTypeArgon2id = 1;

        public const int MasterKeySize = 32;
        public const int EncryptedMasterKeySize = AesGcmHelper.NonceSize + MasterKeySize + AesGcmHelper.TagSize;

        public int KdfType { get; }
        public Argon2Settings Settings { get; }
        public byte[] Salt { get; }
        public byte[] EncryptedMasterKey { get; }

        /// <summary>
        /// Used when loading a box from a file
        /// </summary>
        public MasterKeyBox(int kdfType, Argon2Settings settings, byte[] salt, byte[] encryptedMasterKey)
        {
            if (kdfType != KdfTypeArgon2id)
            {
                throw new NotSupportedException("Unknown KDF type: " + kdfType);
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (salt == null || salt.Length != KeyDerivation.SaltSize)
            {
                throw new ArgumentException("Salt must be " + KeyDerivation.SaltSize + " bytes.", nameof(salt));
            }
            if (encryptedMasterKey == null || encryptedMasterKey.Length != EncryptedMasterKeySize)
            {
                throw new ArgumentException("Encrypted master key must be " + EncryptedMasterKeySize + " bytes.", nameof(encryptedMasterKey));
            }

            settings.Validate();

            KdfType = kdfType;
            Settings = settings;
            Salt = salt;
            EncryptedMasterKey = encryptedMasterKey;
        }

        // Making a new box

        /// <summary>Box 1 - lock the master key with the password.</summary>
        public static MasterKeyBox LockWithPassword(byte[] masterKey, string password, Argon2Settings settings)
        {
            CheckMasterKey(masterKey);

            // A new salt every time we lock. So changing back to an old
            // password still gives a completely different box key.
            byte[] salt = KeyDerivation.CreateSalt();
            byte[] boxKey = KeyDerivation.DeriveKeyFromPassword(password, salt, settings);

            return LockWithBoxKey(masterKey, boxKey, salt, settings);
        }

        /// <summary>Box 2 - lock the master key with the bytes from the 24 words.</summary>
        public static MasterKeyBox LockWithSecretBytes(byte[] masterKey, byte[] secretBytes, Argon2Settings settings)
        {
            CheckMasterKey(masterKey);

            byte[] salt = KeyDerivation.CreateSalt();
            byte[] boxKey = KeyDerivation.DeriveKeyFromBytes(secretBytes, salt, settings);

            return LockWithBoxKey(masterKey, boxKey, salt, settings);
        }

        private static MasterKeyBox LockWithBoxKey(byte[] masterKey, byte[] boxKey, byte[] salt, Argon2Settings settings)
        {
            try
            {
                // No associated data here. If someone edits the salt or the
                // settings, Argon2 gives a different box key and the unlock
                // simply fails. The whole box is also protected later as
                // associated data of the password list
                byte[] encryptedMasterKey = AesGcmHelper.Encrypt(boxKey, masterKey, null);
                return new MasterKeyBox(KdfTypeArgon2id, settings, salt, encryptedMasterKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(boxKey);
            }
        }

        // unlocking the box

        /// <summary>
        /// Box 1: try to open with a password.
        /// Returns false if the password is wrong
        /// Works like int.TryParse.
        /// </summary>
        public bool TryUnlockWithPassword(string password, out byte[] masterKey)
        {
            byte[] boxKey = KeyDerivation.DeriveKeyFromPassword(password, Salt, Settings);
            return TryUnlockWithBoxKey(boxKey, out masterKey);
        }

        /// <summary>
        /// Box 2: try to open with the bytes from the 24 words.
        /// Returns false if the bytes are wrong
        /// </summary>
        public bool TryUnlockWithSecretBytes(byte[] secretBytes, out byte[] masterKey)
        {
            byte[] boxKey = KeyDerivation.DeriveKeyFromBytes(secretBytes, Salt, Settings);
            return TryUnlockWithBoxKey(boxKey, out masterKey);
        }

        private bool TryUnlockWithBoxKey(byte[] boxKey, out byte[] masterKey)
        {
            try
            {
                masterKey = AesGcmHelper.Decrypt(boxKey, EncryptedMasterKey, null);
                return true;
            }
            catch (CryptographicException)
            {
                masterKey = Array.Empty<byte>();
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(boxKey);
            }
        }

        private static void CheckMasterKey(byte[] masterKey)
        {
            if (masterKey == null || masterKey.Length != MasterKeySize)
            {
                throw new ArgumentException("Master key must be " + MasterKeySize + " bytes.", nameof(masterKey));
            }
        }
    }
}
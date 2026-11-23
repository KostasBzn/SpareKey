using SpareKey.Core.Crypto;
using System.Security.Cryptography;
using System.Text;

namespace SpareKey.Core.Storage
{
    /// <summary>
    /// Turns the boxes + encrypted data into the bytes of a .sparekey file,
    /// and reads them back. This class decides the exact file layout.
    /// It does not touch the disk: SafeFileWriter does the writing,
    /// Vault does the reading.
    /// </summary>
    internal class VaultFile
    {
        /* FILE LAYOUT (format version 1).
         *
         * -----------------------------------------------------------------
         * HEADER     | "SPAREKEY" (8 bytes, ASCII) | VERSION (2 bytes)    |
         * -----------------------------------------------------------------
         * BOX 1      | KDF TYPE (1) | MEMORY KiB (4) | ITERATIONS (4)     |
         * (password) | PARALLELISM (4) | SALT (16) | ENCRYPTED KEY (60)   |
         * -----------------------------------------------------------------
         * BOX 2      | same layout as Box 1                               |
         * (24 words) |                                                    |
         * -----------------------------------------------------------------
         * DATA       | LENGTH (4) | NONCE (12) | CIPHERTEXT | TAG (16)    |
         * -----------------------------------------------------------------
         */

        private const string MagicText = "SPAREKEY";
        public const ushort CurrentVersion = 1;

        public ushort Version { get; }
        public MasterKeyBox PasswordBox { get; }
        public MasterKeyBox RecoveryBox { get; }

        // Kept exactly as read from the file. To decrypt we must use the
        // very same bytes as associated data, not a rebuilt copy.
        private readonly byte[] associatedData;
        private readonly byte[] encryptedData;

        private VaultFile(ushort version, MasterKeyBox passwordBox, MasterKeyBox recoveryBox, byte[] associatedData, byte[] encryptedData)
        {
            Version = version;
            PasswordBox = passwordBox;
            RecoveryBox = recoveryBox;
            this.associatedData = associatedData;
            this.encryptedData = encryptedData;
        }

        /// <summary>
        /// Builds the complete file bytes. Every call encrypts again with a
        /// new nonce
        /// Always writes the CURRENT version, so saving an old file upgrades it.
        /// </summary>
        public static byte[] Build(MasterKeyBox passwordBox, MasterKeyBox recoveryBox, byte[] masterKey, byte[] plainData)
        {
            // Step 1: header + both boxes. These bytes become the associated data.
            byte[] headerAndBoxes;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes(MagicText));
                writer.Write(CurrentVersion);
                WriteBox(writer, passwordBox);
                WriteBox(writer, recoveryBox);
                writer.Flush();
                headerAndBoxes = stream.ToArray();
            }

            // Step 2: encrypt the password list
            byte[] encrypted = AesGcmHelper.Encrypt(masterKey, plainData, headerAndBoxes);

            // Step 3: put it all together
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(headerAndBoxes);
                writer.Write(encrypted.Length);
                writer.Write(encrypted);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteBox(BinaryWriter writer, MasterKeyBox box)
        {
            writer.Write((byte)box.KdfType);
            writer.Write(box.Settings.MemoryKiB);
            writer.Write(box.Settings.Iterations);
            writer.Write(box.Settings.Parallelism);
            writer.Write(box.Salt);
            writer.Write(box.EncryptedMasterKey);
        }

        /// <summary>
        /// Reads the file layout. Does NOT decrypt anything yet, so no
        /// password is needed here. Throws InvalidDataException if the
        /// file is not a SpareKey file or is damaged.
        /// </summary>
        public static VaultFile Parse(byte[] fileBytes)
        {
            if (fileBytes == null)
            {
                throw new ArgumentNullException(nameof(fileBytes));
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(fileBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    string magic = Encoding.ASCII.GetString(reader.ReadBytes(MagicText.Length));
                    if (magic != MagicText)
                    {
                        throw new InvalidDataException("This is not a SpareKey vault file.");
                    }

                    // "New versions must ALWAYS open old files."
                    // If version 2 comes, we add "case 2:" here and keep "case 1:".
                    ushort version = reader.ReadUInt16();
                    MasterKeyBox passwordBox;
                    MasterKeyBox recoveryBox;
                    switch (version)
                    {
                        case 1:
                            passwordBox = ReadBox(reader);
                            recoveryBox = ReadBox(reader);
                            break;
                        default:
                            throw new InvalidDataException("This vault was made by a newer version of SpareKey (file format " + version + "). Please update the app.");
                    }

                    // Everything read so far = the associated data
                    int headerAndBoxesLength = (int)stream.Position;
                    byte[] headerAndBoxes = new byte[headerAndBoxesLength];
                    Array.Copy(fileBytes, 0, headerAndBoxes, 0, headerAndBoxesLength);

                    int dataLength = reader.ReadInt32();
                    long bytesLeft = stream.Length - stream.Position;
                    if (dataLength < AesGcmHelper.Overhead || dataLength != bytesLeft)
                    {
                        throw new InvalidDataException("The vault file is damaged (wrong data length).");
                    }
                    byte[] encrypted = reader.ReadBytes(dataLength);

                    return new VaultFile(version, passwordBox, recoveryBox, headerAndBoxes, encrypted);
                }
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("The vault file is damaged (it ends too early).");
            }
            catch (ArgumentException ex)
            {
                // MasterKeyBox / Argon2Settings complain with ArgumentException
                // when the values in the file make no sense.
                throw new InvalidDataException("The vault file is damaged (" + ex.Message + ")", ex);
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidDataException("The vault file uses something this version doesn't know (" + ex.Message + ")", ex);
            }
        }

        private static MasterKeyBox ReadBox(BinaryReader reader)
        {
            int kdfType = reader.ReadByte();

            Argon2Settings settings = new Argon2Settings();
            settings.MemoryKiB = reader.ReadInt32();
            settings.Iterations = reader.ReadInt32();
            settings.Parallelism = reader.ReadInt32();

            byte[] salt = reader.ReadBytes(KeyDerivation.SaltSize);
            byte[] encryptedMasterKey = reader.ReadBytes(MasterKeyBox.EncryptedMasterKeySize);

            // The constructor checks every value (sizes, settings limits)
            return new MasterKeyBox(kdfType, settings, salt, encryptedMasterKey);
        }

        /// <summary>
        /// Decrypts the password list. Call this only AFTER a box opened,
        /// with the master key from that box.
        /// </summary>
        public byte[] DecryptData(byte[] masterKey)
        {
            try
            {
                return AesGcmHelper.Decrypt(masterKey, encryptedData, associatedData);
            }
            catch (CryptographicException)
            {
                // The box opened, so the password was right. If the data still
                // fails, something in the file was changed or damaged.
                // This is exactly the case the associated data is there to catch.
                throw new InvalidDataException("The vault file was changed or is damaged. Try opening a backup.");
            }
        }
    }
}
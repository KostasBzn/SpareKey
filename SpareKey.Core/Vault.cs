using SpareKey.Core.Crypto;
using SpareKey.Core.Storage;
using System.Security.Cryptography;
using System.Text.Json;

namespace SpareKey.Core
{
    /// <summary>
    /// The only class the UI talks to. Everything crypto stays behind it.
    ///
    /// Usage:
    ///   Vault vault = Vault.Create(path, password, out string[] words); // first time
    ///   Vault vault = Vault.OpenWithPassword(path, password); // every day
    ///   vault.AddEntry(...); vault.Save();
    ///   vault.Lock(); // auto-lock
    ///
    /// Create, Open, Recover and ChangePassword run Argon2 and take about
    /// a second. Recommended task.Run
    /// </summary>
    public class Vault
    {
        private const int BackupsToKeep = 5;

        private readonly string filePath;
        private MasterKeyBox passwordBox;
        private readonly MasterKeyBox recoveryBox;
        private byte[] masterKey;
        private readonly List<VaultEntry> entries;

        public string FilePath
        {
            get { return filePath; }
        }

        public IReadOnlyList<VaultEntry> Entries
        {
            get { return entries; }
        }

        public bool HasUnsavedChanges { get; private set; }
        public bool IsLocked { get; private set; }

        private Vault(string filePath, MasterKeyBox passwordBox, MasterKeyBox recoveryBox, byte[] masterKey, List<VaultEntry> entries)
        {
            this.filePath = filePath;
            this.passwordBox = passwordBox;
            this.recoveryBox = recoveryBox;
            this.masterKey = masterKey;
            this.entries = entries;
        }

        /// <summary>
        /// Makes a new, empty vault and saves it right away.
        /// recoveryWords: the 24 words. Show them to the user ONCE and never
        /// store them anywhere. This is the only time they exist in the app.
        /// </summary>
        public static Vault Create(string filePath, string password, out string[] recoveryWords)
        {
            if (File.Exists(filePath))
            {
                // Never overwrite an existing vault by accident
                throw new IOException("A file already exists at " + filePath);
            }

            Argon2Settings settings = Argon2Settings.CreateDefault();

            // The master key: made once, here, and never changes again
            byte[] masterKey = RandomNumberGenerator.GetBytes(MasterKeyBox.MasterKeySize);

            recoveryWords = RecoveryWords.CreateNewWords();
            byte[] recoverySecret = RecoveryWords.WordsToBytes(recoveryWords);

            MasterKeyBox passwordBox = MasterKeyBox.LockWithPassword(masterKey, password, settings);
            MasterKeyBox recoveryBox = MasterKeyBox.LockWithSecretBytes(masterKey, recoverySecret, settings);
            CryptographicOperations.ZeroMemory(recoverySecret);

            Vault vault = new Vault(filePath, passwordBox, recoveryBox, masterKey, new List<VaultEntry>());
            vault.Save();
            return vault;
        }

        /// <summary>
        /// Opens with the password (Box 1).
        /// Returns null if the password is wrong.
        /// Throws InvalidDataException if the file is damaged or was changed.
        /// </summary>
        public static Vault OpenWithPassword(string filePath, string password)
        {
            VaultFile file = VaultFile.Parse(File.ReadAllBytes(filePath));

            byte[] masterKey;
            if (!file.PasswordBox.TryUnlockWithPassword(password, out masterKey))
            {
                return null;
            }

            return OpenWithMasterKey(filePath, file, masterKey);
        }

        /// <summary>
        /// Opens with the 24 words (Box 2).
        /// Returns null if the words are valid but belong to another vault.
        /// Throws FormatException for a typo
        /// Throws InvalidDataException if the file is damaged or was changed.
        /// </summary>
        public static Vault OpenWithRecoveryWords(string filePath, string[] words)
        {
            // Check the words first: a typo gives a clear message
            // without waiting for Argon2.
            byte[] recoverySecret = RecoveryWords.WordsToBytes(words);

            VaultFile file = VaultFile.Parse(File.ReadAllBytes(filePath));

            byte[] masterKey;
            bool opened = file.RecoveryBox.TryUnlockWithSecretBytes(recoverySecret, out masterKey);
            CryptographicOperations.ZeroMemory(recoverySecret);
            if (!opened)
            {
                return null;
            }

            return OpenWithMasterKey(filePath, file, masterKey);
        }

        /// <summary>
        /// "Forgot password": open with the 24 words and set a new password.
        /// Saves right away. Returns null if the words don't open this vault.
        /// </summary>
        public static Vault Recover(string filePath, string[] words, string newPassword)
        {
            Vault vault = OpenWithRecoveryWords(filePath, words);
            if (vault == null)
            {
                return null;
            }

            vault.ChangePassword(newPassword);
            return vault;
        }

        private static Vault OpenWithMasterKey(string filePath, VaultFile file, byte[] masterKey)
        {
            byte[] json = file.DecryptData(masterKey);
            try
            {
                List<VaultEntry> loaded = JsonSerializer.Deserialize<List<VaultEntry>>(json);
                if (loaded == null)
                {
                    loaded = new List<VaultEntry>();
                }
                return new Vault(filePath, file.PasswordBox, file.RecoveryBox, masterKey, loaded);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }

        public void AddEntry(VaultEntry entry)
        {
            CheckNotLocked();
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            entry.CreatedUtc = DateTime.UtcNow;
            entry.ModifiedUtc = entry.CreatedUtc;
            entries.Add(entry);
            HasUnsavedChanges = true;
        }

        /// <summary>
        /// Replaces the entry that has the same Id.
        /// </summary>
        public void UpdateEntry(VaultEntry updatedEntry)
        {
            CheckNotLocked();
            if (updatedEntry == null)
            {
                throw new ArgumentNullException(nameof(updatedEntry));
            }

            int position = FindPosition(updatedEntry.Id);
            if (position == -1)
            {
                throw new KeyNotFoundException("No entry with this id.");
            }

            updatedEntry.CreatedUtc = entries[position].CreatedUtc;
            updatedEntry.ModifiedUtc = DateTime.UtcNow;
            entries[position] = updatedEntry;
            HasUnsavedChanges = true;
        }

        public void DeleteEntry(Guid id)
        {
            CheckNotLocked();

            int position = FindPosition(id);
            if (position == -1)
            {
                throw new KeyNotFoundException("No entry with this id.");
            }

            entries.RemoveAt(position);
            HasUnsavedChanges = true;
        }

        private int FindPosition(Guid id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }


        /// <summary>
        /// Encrypts everything with a new nonce and writes it safely
        /// (temp file to rename, with backups).
        /// </summary>
        public void Save()
        {
            CheckNotLocked();

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(entries);
            try
            {
                byte[] fileBytes = VaultFile.Build(passwordBox, recoveryBox, masterKey, json);
                SafeFileWriter.Write(filePath, fileBytes, BackupsToKeep);
                HasUnsavedChanges = false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }

        /// <summary>
        /// Re-locks Box 1 with the new password and saves right away.
        /// The master key and Box 2 (24 words) stay the same.
        /// </summary>
        public void ChangePassword(string newPassword)
        {
            CheckNotLocked();

            // Uses today's default settings, so an old vault gets stronger
            // Argon2 settings whenever the password is changed.
            passwordBox = MasterKeyBox.LockWithPassword(masterKey, newPassword, Argon2Settings.CreateDefault());
            Save();
        }

        /// <summary>
        /// Wipes the master key and forgets the entries. After this we have to re open the file with password.
        /// Unsaved changes are lost if we dont call Save() before
        /// </summary>
        public void Lock()
        {
            if (IsLocked)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(masterKey);
            masterKey = Array.Empty<byte>();

            // The password strings themselves can't be wiped in .NET,
            // we can only drop our references to them (see Known limits).
            entries.Clear();
            IsLocked = true;
        }

        private void CheckNotLocked()
        {
            if (IsLocked)
            {
                throw new InvalidOperationException("The vault is locked. Open it again first.");
            }
        }
    }
}
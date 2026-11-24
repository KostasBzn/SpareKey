namespace SpareKey.Core.Tests
{
    /// <summary>
    /// the basic tests that protect the data.
    /// </summary>
    public class VaultTests : IDisposable
    {
        private const string Password = "test-password";

        private readonly string testFolder;
        private readonly string vaultPath;

        public VaultTests()
        {
            testFolder = Path.Combine(Path.GetTempPath(), "SpareKeyTests-" + Guid.NewGuid());
            Directory.CreateDirectory(testFolder);
            vaultPath = Path.Combine(testFolder, "test.sparekey");
        }

        public void Dispose()
        {
            if (Directory.Exists(testFolder))
            {
                Directory.Delete(testFolder, true);
            }
        }

        [Fact]
        public void Create_ThenOpenWithPassword_GivesBackTheSameEntries()
        {
            Vault vault = Vault.Create(vaultPath, Password, out string[] words);
            VaultEntry entry = new VaultEntry();
            entry.Title = "GitHub";
            entry.Username = "me@example.com";
            entry.Password = "p@ss wörd";   // non-English characters on purpose
            vault.AddEntry(entry);
            vault.Save();
            vault.Lock();

            Vault? reopened = Vault.OpenWithPassword(vaultPath, Password);

            Assert.NotNull(reopened);
            Assert.Single(reopened.Entries);
            Assert.Equal("GitHub", reopened.Entries[0].Title);
            Assert.Equal("me@example.com", reopened.Entries[0].Username);
            Assert.Equal("p@ss wörd", reopened.Entries[0].Password);
        }

        [Fact]
        public void OpenWithPassword_WrongPassword_ReturnsNull()
        {
            Vault.Create(vaultPath, Password, out string[] words);

            Vault? result = Vault.OpenWithPassword(vaultPath, "wrong-password");

            Assert.Null(result);
        }

        [Fact]
        public void OpenWithPassword_OneByteChangedInPasswordList_Throws()
        {
            Vault.Create(vaultPath, Password, out string[] words);

            // The last byte of the file is part of the tag of the password list
            FlipOneByte(vaultPath, new FileInfo(vaultPath).Length - 1);

            Assert.Throws<InvalidDataException>(() => Vault.OpenWithPassword(vaultPath, Password));
        }

        [Fact]
        public void OpenWithPassword_OneByteChangedInBox2_Throws()
        {
            Vault.Create(vaultPath, Password, out string[] words);
            FlipOneByte(vaultPath, 150);

            Assert.Throws<InvalidDataException>(() => Vault.OpenWithPassword(vaultPath, Password));
        }

        [Fact]
        public void OpenWithRecoveryWords_RightWords_Opens()
        {
            Vault.Create(vaultPath, Password, out string[] words);

            Vault? result = Vault.OpenWithRecoveryWords(vaultPath, words);

            Assert.NotNull(result);
        }

        [Fact]
        public void Recover_SetsNewPassword_OldPasswordStopsWorking()
        {
            Vault.Create(vaultPath, Password, out string[] words);

            Vault? recovered = Vault.Recover(vaultPath, words, "new-password");

            Assert.NotNull(recovered);
            Assert.NotNull(Vault.OpenWithPassword(vaultPath, "new-password"));
            Assert.Null(Vault.OpenWithPassword(vaultPath, Password));
            // The 24 words must still work after a password change
            Assert.NotNull(Vault.OpenWithRecoveryWords(vaultPath, words));
        }

        [Fact]
        public void OpenWithRecoveryWords_Typo_ThrowsFormatException()
        {
            Vault.Create(vaultPath, Password, out string[] words);
            words[5] = "notaword";

            FormatException error = Assert.Throws<FormatException>(() => Vault.OpenWithRecoveryWords(vaultPath, words));

            // The message should tell the user which word is wrong
            Assert.Contains("Word 6", error.Message);
        }

        // Changes one bit of one byte in the file, like disk damage or tampering
        private static void FlipOneByte(string path, long position)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[position] = (byte)(bytes[position] ^ 1);
            File.WriteAllBytes(path, bytes);
        }
    }
}

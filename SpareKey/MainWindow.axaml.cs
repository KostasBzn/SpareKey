using System;
using System.IO;
using Avalonia.Controls;
using SpareKey.Core;
using SpareKey.Services;
using SpareKey.Views;

namespace SpareKey
{
    /// <summary>
    /// The only window. It shows one view at a time
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int AutoLockMinutes = 5;

        private readonly ClipboardService clipboard;
        private readonly AutoLockService autoLock;

        // The open vault, or null while locked
        private Vault currentVault;

        // Used by OnClosing, see there
        private bool readyToClose;

        public MainWindow()
        {
            InitializeComponent();

            clipboard = new ClipboardService(this);
            autoLock = new AutoLockService(this, AutoLockMinutes);
            autoLock.LockRequested += OnLockRequested;

            string startPath = "";
#if DEBUG
            startPath = GetOrCreateTestVault();
#endif
            ShowUnlockView(startPath);
        }

        private void ShowUnlockView(string vaultPath)
        {
            UnlockView unlockView = new UnlockView();
            unlockView.VaultUnlocked += OnVaultUnlocked;
            unlockView.SetVaultPath(vaultPath);

            Content = unlockView;
        }

        private void OnVaultUnlocked(object sender, Vault vault)
        {
            currentVault = vault;

            VaultView vaultView = new VaultView();
            vaultView.SetVault(vault, clipboard);
            vaultView.LockClicked += OnLockRequested;

            Content = vaultView;
            autoLock.Start();
        }

        // Lock button AND autolock both end up here
        private async void OnLockRequested(object sender, EventArgs e)
        {
            string vaultPath = LockVault();
            await clipboard.ClearIfOursAsync();
            ShowUnlockView(vaultPath);
        }

        /// <summary>
        /// Saves if needed, wipes the master key, forgets the vault.
        /// </summary>
        private string LockVault()
        {
            autoLock.Stop();

            if (currentVault == null)
                return "";

            string path = currentVault.FilePath;
            // if that save failed. Locking still happens either way.
            if (currentVault.HasUnsavedChanges)
            {
                try
                {
                    currentVault.Save();
                }
                catch (Exception) { }
            }

            currentVault.Lock();
            currentVault = null;
            return path;
        }

        // When the window closes: lock the vault and clear the clipboard.
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (readyToClose)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            LockVault();
            await clipboard.ClearIfOursAsync();

            readyToClose = true;
            Close();
        }

#if DEBUG
        // TEMPORARY test helper, only in Debug builds
        private static string GetOrCreateTestVault()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "test.sparekey");

            if (!File.Exists(path))
            {
                Vault vault = Vault.Create(path, "pass1234", out string[] words);

                VaultEntry entry = new VaultEntry();
                entry.Title = "facebook";
                entry.Username = "fb@example.com";
                entry.Password = "12345";
                vault.AddEntry(entry);

                vault.Save();
                vault.Lock();
            }

            return path;
        }
#endif
    }
}

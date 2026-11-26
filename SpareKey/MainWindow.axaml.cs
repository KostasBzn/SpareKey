using System;
using System.IO;
using Avalonia.Controls;
using SpareKey.Core;
using SpareKey.Services;
using SpareKey.Views;

namespace SpareKey
{
    /// <summary>
    /// The only window. It shows one view at a time and switches between them
    ///
    /// It also owns the clipboard, auto-lock and the settings file.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int AutoLockMinutes = 5;

        private readonly AppSettings settings;
        private readonly ClipboardService clipboard;
        private readonly AutoLockService autoLock;

        // The open vault, or null while locked
        private Vault currentVault;

        // Used by OnClosing, see there
        private bool readyToClose;

        public MainWindow()
        {
            InitializeComponent();
            Version version = typeof(MainWindow).Assembly.GetName().Version;
            Title = "SpareKey v" + version.ToString(2);

            settings = AppSettings.Load();
            clipboard = new ClipboardService(this);
            autoLock = new AutoLockService(this, AutoLockMinutes);
            autoLock.LockRequested += OnLockRequested;

            ShowStartScreen();
        }

        // Screens

        private void ShowStartScreen()
        {
            string lastPath = settings.LastVaultPath;

            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
                ShowUnlockView(lastPath);
            else
                ShowSetupView();
        }

        private void ShowUnlockView(string vaultPath)
        {
            UnlockView unlockView = new UnlockView();
            unlockView.VaultUnlocked += OnVaultOpened;
            unlockView.RecoveryRequested += OnRecoveryRequested;
            unlockView.CreateNewRequested += OnCreateNewRequested;
            unlockView.SetVaultPath(vaultPath);

            Content = unlockView;
        }

        private void ShowSetupView()
        {
            SetupView setupView = new SetupView();
            setupView.VaultCreated += OnVaultOpened;
            setupView.OpenExistingRequested += OnOpenExistingRequested;

            Content = setupView;
        }

        private void ShowRecoveryView(string vaultPath)
        {
            RecoveryView recoveryView = new RecoveryView();
            recoveryView.VaultRecovered += OnVaultOpened;
            recoveryView.BackRequested += OnRecoveryBackRequested;
            recoveryView.SetVaultPath(vaultPath);

            Content = recoveryView;
        }

        // Events from the views

        // Unlock, setup and recovery all end here when the vault is open
        private void OnVaultOpened(object sender, Vault vault)
        {
            currentVault = vault;

            // Remember this vault for the next start
            settings.LastVaultPath = vault.FilePath;
            settings.Save();

            VaultView vaultView = new VaultView();
            vaultView.SetVault(vault, clipboard);
            vaultView.LockClicked += OnLockRequested;

            Content = vaultView;
            autoLock.Start();
        }

        private void OnRecoveryRequested(object sender, string vaultPath)
        {
            ShowRecoveryView(vaultPath);
        }

        private void OnCreateNewRequested(object sender, EventArgs e)
        {
            ShowSetupView();
        }

        private void OnOpenExistingRequested(object sender, EventArgs e)
        {
            ShowUnlockView("");
        }

        private void OnRecoveryBackRequested(object sender, EventArgs e)
        {
            ShowUnlockView(settings.LastVaultPath);
        }

        // Lock button Aand auto-lock call  this event
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

        // When the window closes lock the vault and clear the clipboard.
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
    }
}

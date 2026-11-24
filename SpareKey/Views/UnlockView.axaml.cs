using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SpareKey.Core;

namespace SpareKey.Views
{
    public partial class UnlockView : UserControl
    {
        /// <summary>
        /// Raised when the vault opened. MainWindow listens to this and
        /// switches to the next screen. Works like a button's Click event.
        /// </summary>
        public event EventHandler<Vault> VaultUnlocked;

        public UnlockView()
        {
            InitializeComponent();

            BrowseButton.Click += OnBrowseClick;
            UnlockButton.Click += OnUnlockClick;
            ForgotButton.Click += OnForgotClick;
        }

        /// <summary>Fills in the vault path.</summary>
        public void SetVaultPath(string path)
        {
            VaultPathBox.Text = path;
        }

        // Put the cursor in the password box as soon as the view is shown
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            PasswordBox.Focus();
        }

        // Buttons

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            FilePickerFileType vaultFiles = new FilePickerFileType("SpareKey vault");
            vaultFiles.Patterns = new string[] { "*.sparekey" };

            FilePickerOpenOptions options = new FilePickerOpenOptions();
            options.Title = "Open SpareKey vault";
            options.AllowMultiple = false;
            options.FileTypeFilter = new List<FilePickerFileType> { vaultFiles, FilePickerFileTypes.All };

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                return;   // we pressed Cancel
            }

            string path = files[0].TryGetLocalPath();
            if (path != null)
            {
                VaultPathBox.Text = path;
                PasswordBox.Focus();
            }
        }

        private async void OnUnlockClick(object sender, RoutedEventArgs e)
        {
            string path = (VaultPathBox.Text ?? "").Trim();
            string password = PasswordBox.Text ?? "";

            // Quick checks first
            if (path == "")
            {
                ShowMessage("Choose a vault file first.");
                return;
            }
            if (!File.Exists(path))
            {
                ShowMessage("File not found.");
                return;
            }
            if (password == "")
            {
                ShowMessage("Enter your password.");
                PasswordBox.Focus();
                return;
            }

            SetBusy(true);
            try
            {
                // Argon takes about a second. Task.Run does the work to avoid the window freeze
                Vault vault = await Task.Run(() => Vault.OpenWithPassword(path, password));

                if (vault == null)
                {
                    ShowMessage("Wrong password.");
                    PasswordBox.Text = "";
                    PasswordBox.Focus();
                    return;
                }

                // Don't keep the password in the box once we're in
                PasswordBox.Text = "";
                VaultUnlocked?.Invoke(this, vault);
            }
            catch (InvalidDataException ex)
            {
                // Damaged or changed file, wrong file type, newer version.
                // Core already wrote a message meant for the user.
                ShowMessage(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                ShowMessage("No permission to read this file.");
            }
            catch (IOException ex)
            {
                ShowMessage("Could not read the file: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OnForgotClick(object sender, RoutedEventArgs e)
        {
            // Recovery screen
            ShowMessage("Recovery with the 24 words is not built yet.");
        }

        // Helpers

        private void ShowMessage(string message)
        {
            MessageText.Text = message;
            MessageText.IsVisible = true;
        }

        private void SetBusy(bool busy)
        {
            UnlockButton.IsEnabled = !busy;
            BrowseButton.IsEnabled = !busy;
            VaultPathBox.IsEnabled = !busy;
            PasswordBox.IsEnabled = !busy;
            UnlockButton.Content = busy ? "Unlocking..." : "Unlock";

            if (busy)
            {
                MessageText.IsVisible = false;
            }
        }
    }
}
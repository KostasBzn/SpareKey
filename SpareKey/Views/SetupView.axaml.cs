using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SpareKey.Core;
using SpareKey.Services;

namespace SpareKey.Views
{
    /// <summary>
    /// First run: create a new vault, show the 24 words, confirm 3 of them.
    /// </summary>
    public partial class SetupView : UserControl
    {
        /// <summary>Raised after the words were confirmed. The vault is open.</summary>
        public event EventHandler<Vault> VaultCreated;

        /// <summary>Raised when the user already has a vault file.</summary>
        public event EventHandler OpenExistingRequested;

        // Kept between the steps. Cleared when setup is finished.
        private Vault createdVault;
        private string[] recoveryWords;
        private int[] confirmPositions;   // 3 word positions, counted from 0

        public SetupView()
        {
            InitializeComponent();

            ChooseButton.Click += OnChooseClick;
            CreateButton.Click += OnCreateClick;
            OpenExistingButton.Click += OnOpenExistingClick;
            WordsDoneButton.Click += OnWordsDoneClick;
            BackToWordsButton.Click += OnBackToWordsClick;
            FinishButton.Click += OnFinishClick;

            VaultPathBox.Text = Path.Combine(AppSettings.GetDefaultVaultFolder(), "vault.sparekey");
            ShowStep(1);
        }

        // Shows one of the three panels
        private void ShowStep(int step)
        {
            CreatePanel.IsVisible = step == 1;
            WordsPanel.IsVisible = step == 2;
            ConfirmPanel.IsVisible = step == 3;

            // Only the button of the visible step reacts to Enter.
            CreateButton.IsDefault = step == 1;
            WordsDoneButton.IsDefault = step == 2;
            FinishButton.IsDefault = step == 3;

            MessageText.IsVisible = false;
        }

        // Step 1: create

        private async void OnChooseClick(object sender, RoutedEventArgs e)
        {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            FilePickerFileType vaultFiles = new FilePickerFileType("SpareKey vault");
            vaultFiles.Patterns = new string[] { "*.sparekey" };

            FilePickerSaveOptions options = new FilePickerSaveOptions();
            options.Title = "Where to save the new vault";
            options.SuggestedFileName = "vault.sparekey";
            options.DefaultExtension = "sparekey";
            options.FileTypeChoices = new List<FilePickerFileType> { vaultFiles };
            options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);

            IStorageFile file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file == null)
                return;   // cancelled

            string path = file.TryGetLocalPath();
            if (path != null)
                VaultPathBox.Text = path;
        }

        private async void OnCreateClick(object sender, RoutedEventArgs e)
        {
            string path = (VaultPathBox.Text ?? "").Trim();
            string password = PasswordBox.Text ?? "";
            string confirmPassword = ConfirmPasswordBox.Text ?? "";

            if (path == "")
            {
                ShowMessage("Choose where to save the vault.");
                return;
            }

            string passwordError = PasswordRules.Check(password, confirmPassword);
            if (passwordError != "")
            {
                ShowMessage(passwordError);
                return;
            }

            CreateButton.IsEnabled = false;
            CreateButton.Content = "Creating...";
            try
            {
                // Make sure the folder exists (e.g. Documents/SpareKey the first time)
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                // Argon2 runs twice here (both boxes), so do it in the background
                string[] words = null;
                Vault vault = await Task.Run(() => Vault.Create(path, password, out words));

                createdVault = vault;
                recoveryWords = words;

                PasswordBox.Text = "";
                ConfirmPasswordBox.Text = "";
                ShowWords();
                ShowStep(2);
            }
            catch (IOException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                ShowMessage("No permission to save in this folder. Choose another one.");
            }
            finally
            {
                CreateButton.IsEnabled = true;
                CreateButton.Content = "Create vault";
            }
        }

        private void OnOpenExistingClick(object sender, RoutedEventArgs e)
        {
            OpenExistingRequested?.Invoke(this, EventArgs.Empty);
        }

        // 2. show the words

        private void ShowWords()
        {
            WordsGrid.Children.Clear();
            for (int i = 0; i < recoveryWords.Length; i++)
            {
                TextBlock wordText = new TextBlock();
                wordText.Text = (i + 1) + ". " + recoveryWords[i];
                wordText.FontSize = 15;
                wordText.Margin = new Thickness(0, 5);
                WordsGrid.Children.Add(wordText);
            }
        }

        private void OnWordsDoneClick(object sender, RoutedEventArgs e)
        {
            confirmPositions = PickThreePositions();

            ConfirmLabel1.Text = "Word #" + (confirmPositions[0] + 1);
            ConfirmLabel2.Text = "Word #" + (confirmPositions[1] + 1);
            ConfirmLabel3.Text = "Word #" + (confirmPositions[2] + 1);

            ConfirmBox1.Text = "";
            ConfirmBox2.Text = "";
            ConfirmBox3.Text = "";

            ShowStep(3);
            ConfirmBox1.Focus();
        }

        // 3. different random positions
        private static int[] PickThreePositions()
        {
            List<int> positions = new List<int>();
            while (positions.Count < 3)
            {
                int position = RandomNumberGenerator.GetInt32(24);
                if (!positions.Contains(position))
                    positions.Add(position);
            }
            positions.Sort();
            return positions.ToArray();
        }

        // 3. confirm

        private void OnBackToWordsClick(object sender, RoutedEventArgs e)
        {
            ShowStep(2);
        }

        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            TextBox[] boxes = new TextBox[] { ConfirmBox1, ConfirmBox2, ConfirmBox3 };

            for (int i = 0; i < 3; i++)
            {
                string typed = (boxes[i].Text ?? "").Trim().ToLowerInvariant();
                string expected = recoveryWords[confirmPositions[i]];

                if (typed != expected)
                {
                    ShowMessage("Word #" + (confirmPositions[i] + 1) + " is not right. Check your paper, or show the words again.");
                    boxes[i].Focus();
                    return;
                }
            }

            // Done
            WordsGrid.Children.Clear();
            recoveryWords = null;

            Vault vault = createdVault;
            createdVault = null;
            VaultCreated?.Invoke(this, vault);
        }

        // Helpers

        private void ShowMessage(string message)
        {
            MessageText.Text = message;
            MessageText.IsVisible = true;
        }
    }
}

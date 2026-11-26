using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpareKey.Core;
using SpareKey.Services;

namespace SpareKey.Views
{
    /// <summary>
    /// "Forgot password": 24 words + new password -> vault is open again.
    /// </summary>
    public partial class RecoveryView : UserControl
    {
        /// <summary>Raised when recovery worked. The vault is open, with the new password.</summary>
        public event EventHandler<Vault> VaultRecovered;

        /// <summary>Raised when the user clicks "Back to unlock".</summary>
        public event EventHandler BackRequested;

        public RecoveryView()
        {
            InitializeComponent();

            RecoverButton.Click += OnRecoverClick;
            BackButton.Click += OnBackClick;
        }

        public void SetVaultPath(string path)
        {
            VaultPathBox.Text = path;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            WordsBox.Focus();
        }

        private async void OnRecoverClick(object sender, RoutedEventArgs e)
        {
            string path = (VaultPathBox.Text ?? "").Trim();
            string[] words = SplitWords(WordsBox.Text ?? "");
            string newPassword = NewPasswordBox.Text ?? "";
            string confirmPassword = ConfirmPasswordBox.Text ?? "";

            if (!File.Exists(path))
            {
                ShowMessage("Vault file not found.");
                return;
            }
            if (words.Length != 24)
            {
                ShowMessage("Expected 24 words, but found " + words.Length + ".");
                return;
            }

            string passwordError = PasswordRules.Check(newPassword, confirmPassword);
            if (passwordError != "")
            {
                ShowMessage(passwordError);
                return;
            }

            SetBusy(true);
            try
            {
                Vault vault = await Task.Run(() => Vault.Recover(path, words, newPassword));

                if (vault == null)
                {
                    ShowMessage("These words are valid, but they don't belong to this vault.");
                    return;
                }

                WordsBox.Text = "";
                NewPasswordBox.Text = "";
                ConfirmPasswordBox.Text = "";
                VaultRecovered?.Invoke(this, vault);
            }
            catch (FormatException ex)
            {
                // unknown word or wrong checksum
                ShowMessage(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                ShowMessage(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                ShowMessage("No permission to change this file.");
            }
            catch (IOException ex)
            {
                ShowMessage("Could not read or save the file: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        // Spaces, tabs and new lines all count as separators
        private static string[] SplitWords(string text)
        {
            char[] separators = new char[] { ' ', '\t', '\r', '\n' };
            return text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        private void ShowMessage(string message)
        {
            MessageText.Text = message;
            MessageText.IsVisible = true;
        }

        private void SetBusy(bool busy)
        {
            RecoverButton.IsEnabled = !busy;
            BackButton.IsEnabled = !busy;
            RecoverButton.Content = busy ? "Recovering..." : "Recover vault";

            if (busy)
                MessageText.IsVisible = false;
        }
    }
}

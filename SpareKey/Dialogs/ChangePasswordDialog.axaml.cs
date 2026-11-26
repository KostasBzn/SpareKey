using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpareKey.Core;
using SpareKey.Services;

namespace SpareKey.Dialogs
{
    /// <summary>
    /// Change the master password of the open vault.
    /// Open with ShowDialog&lt;bool&gt;: true = password was changed.
    /// </summary>
    public partial class ChangePasswordDialog : Window
    {
        private Vault vault;

        public ChangePasswordDialog()
        {
            InitializeComponent();

            ChangeButton.Click += OnChangeClick;
            CancelButton.Click += OnCancelClick;
        }

        /// <summary>Call before ShowDialog.</summary>
        public void SetVault(Vault vault)
        {
            this.vault = vault;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            CurrentPasswordBox.Focus();
        }

        private async void OnChangeClick(object sender, RoutedEventArgs e)
        {
            string currentPassword = CurrentPasswordBox.Text ?? "";
            string newPassword = NewPasswordBox.Text ?? "";
            string confirmPassword = ConfirmPasswordBox.Text ?? "";

            string passwordError = PasswordRules.Check(newPassword, confirmPassword);
            if (passwordError != "")
            {
                ShowMessage(passwordError);
                return;
            }

            SetBusy(true);
            try
            {
                // Ask for the current password first, so nobody can change it
                // just because the app open
                string path = vault.FilePath;
                bool currentIsRight = await Task.Run(() => IsPasswordRight(path, currentPassword));
                if (!currentIsRight)
                {
                    ShowMessage("The current password is wrong.");
                    CurrentPasswordBox.Text = "";
                    CurrentPasswordBox.Focus();
                    return;
                }
                // The window freezes a little because we run on ui thread
                // but its on purpose, to freeze the timer and prevent the auto lock.
                vault.ChangePassword(newPassword);

                Close(true);
            }
            catch (Exception ex)
            {
                ShowMessage("Could not change the password: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Opens the file a second time only to test the password, and locks it immediatelly
        private static bool IsPasswordRight(string path, string password)
        {
            if (password == "")
                return false;

            Vault check = Vault.OpenWithPassword(path, password);
            if (check == null)
                return false;

            check.Lock();
            return true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void ShowMessage(string message)
        {
            MessageText.Text = message;
            MessageText.IsVisible = true;
        }

        private void SetBusy(bool busy)
        {
            ChangeButton.IsEnabled = !busy;
            CancelButton.IsEnabled = !busy;
            ChangeButton.Content = busy ? "Changing..." : "Change password";

            if (busy)
                MessageText.IsVisible = false;
        }
    }
}

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using SpareKey.Core;
using SpareKey.Views;

namespace SpareKey
{
    /// <summary>
    /// The only window. It shows one view at a time:
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowUnlockView();
        }

        private void ShowUnlockView()
        {
            UnlockView unlockView = new UnlockView();
            unlockView.VaultUnlocked += OnVaultUnlocked;

#if DEBUG
            unlockView.SetVaultPath(GetOrCreateTestVault());
#endif

            Content = unlockView;
        }

        private void OnVaultUnlocked(object sender, Vault vault)
        {
            // TEMPORARY until VaultView exists (next step)
            TextBlock placeholder = new TextBlock();
            placeholder.Text = "Unlocked: " + vault.Entries.Count + " entries";
            placeholder.FontSize = 20;
            placeholder.HorizontalAlignment = HorizontalAlignment.Center;
            placeholder.VerticalAlignment = VerticalAlignment.Center;

            Content = placeholder;
        }

#if DEBUG
        // TEMPORARY test helper
        // Makes a test vault next to the exe the first time you run the app.
        //   File:     bin\Debug\net10.0\test.sparekey
        //   Password: test-password
        private static string GetOrCreateTestVault()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "test.sparekey");

            if (!File.Exists(path))
            {
                Vault vault = Vault.Create(path, "test-password", out string[] words);

                VaultEntry entry = new VaultEntry();
                entry.Title = "Example";
                entry.Username = "me@example.com";
                entry.Password = "not-a-real-password";
                vault.AddEntry(entry);

                vault.Save();
                vault.Lock();
            }

            return path;
        }
#endif
    }
}
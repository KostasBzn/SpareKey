using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SpareKey.Core;
using SpareKey.Dialogs;
using SpareKey.Services;

namespace SpareKey.Views
{
    /// <summary>
    /// The main screen while unlocked list, search, add/edit/delete, copy.
    /// Every change is saved right away.
    /// </summary>
    public partial class VaultView : UserControl
    {
        /// <summary>Raised when the user clicks Lock. MainWindow does the locking.</summary>
        public event EventHandler LockClicked;

        private Vault vault;
        private ClipboardService clipboard;

        // Delete needs two clicks: the first one only "arms" the button
        private bool deleteArmed;

        public VaultView()
        {
            InitializeComponent();

            SearchBox.TextChanged += OnSearchChanged;
            EntryList.SelectionChanged += OnSelectionChanged;
            EntryList.DoubleTapped += OnEntryDoubleTapped;

            AddButton.Click += OnAddClick;
            ChangePasswordButton.Click += OnChangePasswordClick;
            LockButton.Click += OnLockClick;
            CopyUsernameButton.Click += OnCopyUsernameClick;
            CopyPasswordButton.Click += OnCopyPasswordClick;
            EditButton.Click += OnEditClick;
            DeleteButton.Click += OnDeleteClick;

            UpdateButtons();
        }

        /// <summary>
        /// Called by MainWindow right after creating the view.
        /// (Not in the constructor: the designer preview needs an empty constructor.)
        /// </summary>
        public void SetVault(Vault vault, ClipboardService clipboard)
        {
            this.vault = vault;
            this.clipboard = clipboard;
            RefreshList();
        }

        // List + search

        private void RefreshList()
        {
            if (vault == null)
                return;

            string search = (SearchBox.Text ?? "").Trim();

            List<VaultEntry> shown = new List<VaultEntry>();
            foreach (VaultEntry entry in vault.Entries)
            {
                if (search == "" || Matches(entry, search))
                    shown.Add(entry);
            }
            shown.Sort(CompareByTitle);

            EntryList.ItemsSource = shown;
            StatusText.Text = shown.Count + " of " + vault.Entries.Count + " entries";
            UpdateButtons();
        }

        private static bool Matches(VaultEntry entry, string search)
        {
            return ContainsText(entry.Title, search)
                || ContainsText(entry.Username, search)
                || ContainsText(entry.Url, search);
        }

        private static bool ContainsText(string text, string search)
        {
            return text != null && text.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByTitle(VaultEntry a, VaultEntry b)
        {
            return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
        }

        private VaultEntry GetSelectedEntry()
        {
            return EntryList.SelectedItem as VaultEntry;
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        // Buttons are only clickable when an entry is selected
        private void UpdateButtons()
        {
            bool hasSelection = GetSelectedEntry() != null;
            CopyUsernameButton.IsEnabled = hasSelection;
            CopyPasswordButton.IsEnabled = hasSelection;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;

            // Selecting something else cancels a half done delete
            deleteArmed = false;
            DeleteButton.Content = "Delete";
        }

        // Copy
        private async void OnCopyUsernameClick(object sender, RoutedEventArgs e)
        {
            VaultEntry entry = GetSelectedEntry();
            if (entry == null)
                return;

            await clipboard.CopyAsync(entry.Username);
            StatusText.Text = "Username copied.";
        }

        private async void OnCopyPasswordClick(object sender, RoutedEventArgs e)
        {
            VaultEntry entry = GetSelectedEntry();
            if (entry == null)
                return;

            await clipboard.CopyAsync(entry.Password);
            StatusText.Text = "Password copied. The clipboard clears in 30 seconds.";
        }

        // Add / edit / delete
        private async void OnAddClick(object sender, RoutedEventArgs e)
        {
            EntryDialog dialog = new EntryDialog();
            bool saved = await dialog.ShowDialog<bool>(GetOwnerWindow());

            // The vault may have auto-locked while the dialog was open
            if (!saved || vault.IsLocked)
                return;

            vault.AddEntry(dialog.Result);
            RefreshList();
            EntryList.SelectedItem = dialog.Result;
            SaveVault();
        }

        private async void OnEditClick(object sender, RoutedEventArgs e)
        {
            VaultEntry entry = GetSelectedEntry();
            if (entry == null)
                return;

            EntryDialog dialog = new EntryDialog();
            dialog.LoadEntry(entry);
            bool saved = await dialog.ShowDialog<bool>(GetOwnerWindow());

            if (!saved || vault.IsLocked)
                return;

            vault.UpdateEntry(dialog.Result);
            RefreshList();
            EntryList.SelectedItem = dialog.Result;
            SaveVault();
        }

        private void OnEntryDoubleTapped(object sender, TappedEventArgs e)
        {
            OnEditClick(sender, e);
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            VaultEntry entry = GetSelectedEntry();
            if (entry == null)
                return;

            // on first click we ask second confirmation
            if (!deleteArmed)
            {
                deleteArmed = true;
                DeleteButton.Content = "Click again to delete";
                return;
            }

            vault.DeleteEntry(entry.Id);
            RefreshList();
            SaveVault();
        }

        private async void OnChangePasswordClick(object sender, RoutedEventArgs e)
        {
            ChangePasswordDialog dialog = new ChangePasswordDialog();
            dialog.SetVault(vault);
            bool changed = await dialog.ShowDialog<bool>(GetOwnerWindow());

            if (changed)
                StatusText.Text = "Password changed.";
        }

        private void OnLockClick(object sender, RoutedEventArgs e)
        {
            LockClicked?.Invoke(this, EventArgs.Empty);
        }

        // Helpers
        private void SaveVault()
        {
            try
            {
                vault.Save();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Could not save: " + ex.Message;
            }
        }

        // Dialogs need the window they belong to
        private Window GetOwnerWindow()
        {
            return TopLevel.GetTopLevel(this) as Window;
        }
    }
}

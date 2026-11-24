using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpareKey.Core;
using SpareKey.Core.Crypto;

namespace SpareKey.Dialogs
{
    /// <summary>
    /// Add or edit one entry.
    /// </summary>
    public partial class EntryDialog : Window
    {
        // Guid.Empty = a new entry. Otherwise the id of the entry being edited.
        private Guid entryId = Guid.Empty;

        /// <summary>The entry with the typed values. Only set after Save.</summary>
        public VaultEntry Result { get; private set; }

        public EntryDialog()
        {
            InitializeComponent();

            GenerateButton.Click += OnGenerateClick;
            SaveButton.Click += OnSaveClick;
            CancelButton.Click += OnCancelClick;
        }

        /// <summary>fills the boxes with an existing entry.</summary>
        public void LoadEntry(VaultEntry entry)
        {
            entryId = entry.Id;
            Title = "Edit entry";

            TitleBox.Text = entry.Title;
            UsernameBox.Text = entry.Username;
            PasswordBox.Text = entry.Password;
            UrlBox.Text = entry.Url;
            NotesBox.Text = entry.Notes;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            TitleBox.Focus();
        }

        private void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            PasswordBox.Text = PasswordGenerator.Generate();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string title = (TitleBox.Text ?? "").Trim();
            if (title == "")
            {
                MessageText.Text = "Title is required.";
                MessageText.IsVisible = true;
                TitleBox.Focus();
                return;
            }

            // When editing, it keeps the same id so Vault knows which one to replace.
            VaultEntry entry = new VaultEntry();
            if (entryId != Guid.Empty)
                entry.Id = entryId;

            entry.Title = title;
            entry.Username = UsernameBox.Text ?? "";
            entry.Password = PasswordBox.Text ?? "";
            entry.Url = UrlBox.Text ?? "";
            entry.Notes = NotesBox.Text ?? "";

            Result = entry;
            Close(true);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}

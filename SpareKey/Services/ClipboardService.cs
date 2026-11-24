using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace SpareKey.Services
{
    /// <summary>
    /// Copies text to the clipboard and clears it again after 30 seconds,
    /// but only if the clipboard still holds what we copied.
    /// </summary>
    internal class ClipboardService
    {
        private const int ClearAfterSeconds = 30;

        private readonly TopLevel window;
        private readonly DispatcherTimer clearTimer;
        private string lastCopiedText = "";

        // In Avalonia the clipboard belongs to the window
        public ClipboardService(TopLevel window)
        {
            this.window = window;
            clearTimer = new DispatcherTimer();
            clearTimer.Interval = TimeSpan.FromSeconds(ClearAfterSeconds);
            clearTimer.Tick += OnClearTimerTick;
        }

        public async Task CopyAsync(string text)
        {
            IClipboard clipboard = window.Clipboard;
            if (clipboard == null)
                return;

            await clipboard.SetTextAsync(text);
            lastCopiedText = text;

            // Start the 30 seconds again on every copy
            clearTimer.Stop();
            clearTimer.Start();
        }

        /// <summary>
        /// Clears the clipboard if it still holds our text.
        /// </summary>
        public async Task ClearIfOursAsync()
        {
            clearTimer.Stop();

            IClipboard clipboard = window.Clipboard;
            if (clipboard == null || lastCopiedText == "")
                return;

            // If we copied something unrelated to our app dont touch it
            string current = await clipboard.TryGetTextAsync();
            if (current == lastCopiedText)
                await clipboard.ClearAsync();

            lastCopiedText = "";
        }

        private async void OnClearTimerTick(object sender, EventArgs e)
        {
            await ClearIfOursAsync();
        }
    }
}
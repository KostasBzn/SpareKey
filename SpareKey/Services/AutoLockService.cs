using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SpareKey.Services
{
    /// <summary>
    /// Raises LockRequested when there was no keyboard or mouse activity
    /// in the window for a few minutes. It doesn't lock anything itself
    /// </summary>
    internal class AutoLockService
    {
        private readonly DispatcherTimer idleTimer;

        public event EventHandler LockRequested;

        public AutoLockService(Window window, int idleMinutes)
        {
            idleTimer = new DispatcherTimer();
            idleTimer.Interval = TimeSpan.FromMinutes(idleMinutes);
            idleTimer.Tick += OnIdleTimerTick;

            // Watch keys and mouse in the whole window
            window.AddHandler(InputElement.KeyDownEvent, OnUserActivity, RoutingStrategies.Tunnel);
            window.AddHandler(InputElement.PointerMovedEvent, OnUserActivity, RoutingStrategies.Tunnel);
            window.AddHandler(InputElement.PointerPressedEvent, OnUserActivity, RoutingStrategies.Tunnel);
        }

        /// <summary>Call when the vault is unlocked.</summary>
        public void Start()
        {
            idleTimer.Stop();
            idleTimer.Start();
        }

        /// <summary>Call when the vault is locked.</summary>
        public void Stop()
        {
            idleTimer.Stop();
        }

        private void OnUserActivity(object sender, RoutedEventArgs e)
        {
            // Only count activity while the timer runs 
            if (idleTimer.IsEnabled)
            {
                idleTimer.Stop();
                idleTimer.Start();
            }
        }

        private void OnIdleTimerTick(object sender, EventArgs e)
        {
            idleTimer.Stop();
            LockRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
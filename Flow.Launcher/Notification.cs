using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Flow.Launcher.Core;
using Flow.Launcher.PluginSDK.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Flow.Launcher
{
    /// <summary>
    /// Requires Windows 10 20H1 (Build 19041) or later.
    /// </summary>
    public class Notification(Logger<Notification> logger)
    {
        private readonly Logger<Notification> _logger = logger;
        private readonly ConcurrentDictionary<string, Action> _notificationActions = new();

        internal void Install()
        {
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var actionId = toastArgs.Argument; // Or use toastArgs.UserInput if using input
                if (_notificationActions.TryGetValue(actionId, out var action))
                {
                    action?.Invoke();
                }
            };
        }

        internal void Uninstall()
        {
            _notificationActions.Clear();
            ToastNotificationManagerCompat.Uninstall();
        }

        public void Show(string title, string subTitle, string iconPath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowInternal(title, subTitle, iconPath);
            });
        }

        private void ShowInternal(string title, string subTitle, string iconPath = null)
        {
            var Icon = !File.Exists(iconPath)
                ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
                : iconPath;

            try
            {
                new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(subTitle)
                    .AddAppLogoOverride(new Uri(Icon))
                    .Show();
            }
            catch (InvalidOperationException e)
            {
                // Windows 11 may have a notification issue
                // likely on 22621.1413 or 22621.1485 judging by post time of #2024
                _logger.LogError(e, $"Notification InvalidOperationException Error");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Notification Error");
            }
        }

        public void ShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowInternalWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            });
        }

        private void ShowInternalWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath = null)
        {
            var Icon = !File.Exists(iconPath)
                ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
                : iconPath;

            try
            {
                var guid = Guid.NewGuid().ToString();
                new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(subTitle)
                    .AddButton(buttonText, ToastActivationType.Background, guid)
                    .AddAppLogoOverride(new Uri(Icon))
                    .Show();
                _notificationActions.AddOrUpdate(guid, buttonAction, (key, oldValue) => buttonAction);
            }
            catch (InvalidOperationException e)
            {
                // Windows 11 may have a notification issue
                // likely on 22621.1413 or 22621.1485 judging by post time of #2024
                _logger.LogError(e, $"Notification InvalidOperationException Error");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Notification Error");
            }
        }
    }
}

using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Flow.Launcher.PluginSDK.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Flow.Launcher.Core;

[SupportedOSPlatform("windows10.0.19041.0")]
public class Notification : IDisposable
{
    private readonly Logger<Notification> _logger;
    private readonly ConcurrentDictionary<string, Action> _notificationActions = new();
    private volatile bool _disposed;

    public Notification(Logger<Notification> logger)
    {
        _logger = logger;
        ToastNotificationManagerCompat.OnActivated += OnNotificationActivated;
    }

    private void OnNotificationActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        string actionId = args.Argument;
        if (_notificationActions.TryRemove(actionId, out Action? action))
        {
            action?.Invoke();
        }
    }

    public void Show(string title, string subTitle, string? iconPath = null)
    {
        iconPath = !File.Exists(iconPath)
            ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
            : iconPath;

        try
        {
            new ToastContentBuilder()
                .AddText(title, hintMaxLines: 1)
                .AddText(subTitle)
                .AddAppLogoOverride(new Uri(iconPath))
                .Show();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to show notification: '{title}'");
        }
    }

    public void ShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string? iconPath = null)
    {
        iconPath = !File.Exists(iconPath)
            ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
            : iconPath;

        try
        {
            string guid = Guid.NewGuid().ToString();
            new ToastContentBuilder()
                .AddText(title, hintMaxLines: 1)
                .AddText(subTitle)
                .AddButton(buttonText, ToastActivationType.Background, guid)
                .AddAppLogoOverride(new Uri(iconPath))
                .Show();
            _notificationActions.AddOrUpdate(guid, buttonAction, (key, oldValue) => buttonAction);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to show notification with button: '{title}'");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, true))
            return;

        ToastNotificationManagerCompat.OnActivated -= OnNotificationActivated;
        _notificationActions.Clear();
    }
}

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Plugin.Explorer.Search;

namespace Flow.Launcher.Plugin.Explorer.Views;

[INotifyPropertyChanged]
public partial class PreviewPanel : UserControl
{
    public string FilePath { get; }
    public string FileName { get; }

    [ObservableProperty]
    private string _fileSize = Localize.Preview_UnknownValue;

    [ObservableProperty]
    private string _createdAt = "";

    [ObservableProperty]
    private string _lastModifiedAt = "";

    [ObservableProperty]
    private ImageSource _previewImage = new BitmapImage();

    private readonly Settings _settings;

    public Visibility FileSizeVisibility => _settings.ShowFileSizeInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility CreatedAtVisibility => _settings.ShowCreatedDateInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility LastModifiedAtVisibility => _settings.ShowModifiedDateInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility FileInfoVisibility =>
        _settings.ShowFileSizeInPreviewPanel ||
        _settings.ShowCreatedDateInPreviewPanel ||
        _settings.ShowModifiedDateInPreviewPanel
        ? Visibility.Visible
        : Visibility.Collapsed;

    public PreviewPanel(Settings settings, string filePath, ResultType type)
    {
        _settings = settings;
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);

        InitializeComponent();

        if (_settings.ShowFileSizeInPreviewPanel)
        {
            if (type == ResultType.File)
            {
                FileSize = GetFileSize(filePath);
            }
            else
            {
                _ = Task.Run(() =>
                {
                    FileSize = GetFolderSize(filePath);
                    OnPropertyChanged(nameof(FileSize));
                }).ConfigureAwait(false);
            }
        }

        if (_settings.ShowCreatedDateInPreviewPanel)
        {
            CreatedAt = type == ResultType.File ?
                GetFileCreatedAt(filePath, _settings.PreviewPanelDateFormat, _settings.PreviewPanelTimeFormat, _settings.ShowFileAgeInPreviewPanel) :
                GetFolderCreatedAt(filePath, _settings.PreviewPanelDateFormat, _settings.PreviewPanelTimeFormat, _settings.ShowFileAgeInPreviewPanel);
        }

        if (_settings.ShowModifiedDateInPreviewPanel)
        {
            LastModifiedAt = type == ResultType.File ?
                GetFileLastModifiedAt(filePath, _settings.PreviewPanelDateFormat, _settings.PreviewPanelTimeFormat, _settings.ShowFileAgeInPreviewPanel) :
                GetFolderLastModifiedAt(filePath, _settings.PreviewPanelDateFormat, _settings.PreviewPanelTimeFormat, _settings.ShowFileAgeInPreviewPanel);
        }

        _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        PreviewImage = await Main.Context.API.ImageLoader.LoadAsync(FilePath, true).ConfigureAwait(false);
    }

    public string GetFileSize(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return ResultManager.ToReadableSize(fileInfo.Length, 2);
        }
        catch (FileNotFoundException)
        {
            Main.Context.Logger.LogError($"File not found: {filePath}");
            return Localize.Preview_UnknownValue;
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.Logger.LogError($"Access denied to file: {filePath}");
            return Localize.Preview_UnknownValue;
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to get file size for {filePath}");
            return Localize.Preview_UnknownValue;
        }
    }

    public static string GetFileCreatedAt(string filePath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var createdDate = File.GetCreationTime(filePath);
            var formattedDate = createdDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(createdDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.Logger.LogError($"File not found: {filePath}");
            return Localize.Preview_UnknownValue;
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.Logger.LogError($"Access denied to file: {filePath}");
            return Localize.Preview_UnknownValue;
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to get file created date for {filePath}");
            return Localize.Preview_UnknownValue;
        }
    }

    public static string GetFileLastModifiedAt(string filePath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var lastModifiedDate = File.GetLastWriteTime(filePath);
            var formattedDate = lastModifiedDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(lastModifiedDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.Logger.LogError($"File not found: {filePath}");
            return Localize.Preview_UnknownValue;
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.Logger.LogError($"Access denied to file: {filePath}");
            return Localize.Preview_UnknownValue;
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to get file modified date for {filePath}");
            return Localize.Preview_UnknownValue;
        }
    }

    public static string GetFolderSize(string folderPath)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            // Use parallel enumeration for better performance
            var directoryInfo = new DirectoryInfo(folderPath);
            long size = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .AsParallel()
                .WithCancellation(timeoutCts.Token)
                .Sum(file => file.Length);

            return ResultManager.ToReadableSize(size, 2);
        }
        catch (FileNotFoundException)
        {
            Main.Context.Logger.LogError($"Folder not found: {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.Logger.LogError($"Access denied to folder: {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        catch (OperationCanceledException)
        {
            Main.Context.Logger.LogError($"Operation timed out while calculating folder size for {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        // For parallel operations, AggregateException may be thrown if any of the tasks fail
        catch (AggregateException ae)
        {
            switch (ae.InnerException)
            {
                case FileNotFoundException:
                    Main.Context.Logger.LogError($"Folder not found: {folderPath}");
                    return Localize.Preview_UnknownValue;
                case UnauthorizedAccessException:
                    Main.Context.Logger.LogError($"Access denied to folder: {folderPath}");
                    return Localize.Preview_UnknownValue;
                case OperationCanceledException:
                    Main.Context.Logger.LogError($"Operation timed out while calculating folder size for {folderPath}");
                    return Localize.Preview_UnknownValue;
                default:
                    Main.Context.Logger.LogError(ae, $"Failed to get folder size for {folderPath}");
                    return Localize.Preview_UnknownValue;
            }
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to get folder size for {folderPath}");
            return Localize.Preview_UnknownValue;
        }
    }

    public static string GetFolderCreatedAt(string folderPath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var createdDate = Directory.GetCreationTime(folderPath);
            var formattedDate = createdDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(createdDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.Logger.LogError($"Folder not found: {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.Logger.LogError($"Access denied to folder: {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to get folder created date for {folderPath}");
            return Localize.Preview_UnknownValue;
        }
    }

    public static string GetFolderLastModifiedAt(string folderPath, string previewPanelDateFormat, string previewPanelTimeFormat, bool showFileAgeInPreviewPanel)
    {
        try
        {
            var lastModifiedDate = Directory.GetLastWriteTime(folderPath);
            var formattedDate = lastModifiedDate.ToString(
                $"{previewPanelDateFormat} {previewPanelTimeFormat}",
                CultureInfo.CurrentCulture
            );

            var result = formattedDate;
            if (showFileAgeInPreviewPanel) result = $"{GetFileAge(lastModifiedDate)} - {formattedDate}";
            return result;
        }
        catch (FileNotFoundException)
        {
            Main.Context.Logger.LogError($"Folder not found: {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        catch (UnauthorizedAccessException)
        {
            Main.Context.Logger.LogError($"Access denied to folder: {folderPath}");
            return Localize.Preview_UnknownValue;
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to get folder modified date for {folderPath}");
            return Localize.Preview_UnknownValue;
        }
    }

    private static string GetFileAge(DateTime fileDateTime)
    {
        var now = DateTime.Now;
        var difference = now - fileDateTime;

        if (difference.TotalDays < 1)
            return Localize.Preview_Today;
        if (difference.TotalDays < 30)
            return Localize.Preview_DaysAgo((int)difference.TotalDays);

        var monthsDiff = (now.Year - fileDateTime.Year) * 12 + now.Month - fileDateTime.Month;
        if (monthsDiff == 1)
            return Localize.Preview_OneMonthAgo;
        if (monthsDiff < 12)
            return Localize.Preview_MonthsAgo(monthsDiff);

        var yearsDiff = now.Year - fileDateTime.Year;
        if (now.Month < fileDateTime.Month || (now.Month == fileDateTime.Month && now.Day < fileDateTime.Day))
            yearsDiff--;

        return yearsDiff == 1 ? Localize.Preview_OneYearAgo : Localize.Preview_YearsAgo(yearsDiff);
    }
}

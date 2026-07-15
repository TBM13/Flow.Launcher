using System.Globalization;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Core.Storage;

/// <summary>
/// Serialize and deserialize an object to and from a file.
/// </summary>
/// <param name="enableBackup">Not recommended for big files.</param>
public abstract class Storage<T>(ILoggerFactory loggerFactory, string filePath, bool enableBackup)
    : ISavable where T : class, new()
{
    private T? _data;

    private readonly string _filePath = filePath;
    private readonly string _backupPath = filePath + ".bak";
    private readonly string _dirPath = Path.GetDirectoryName(filePath)
        ?? throw new ArgumentException(null, nameof(filePath));
    private readonly bool _enableBackup = enableBackup;
    private readonly PluginSDK.Logging.Logger<Storage<T>> _logger = new(loggerFactory);

    /// <summary>
    /// Reads the file and deserializes it.
    /// <para/>
    /// When something goes wrong, creates a new instance.
    /// </summary>
    /// <remarks>If this was already called, returns the existing instance.</remarks>
    public T TryLoad()
    {
        if (_data is not null)
            return _data;

        _data = TryLoad(_filePath);
        if (_enableBackup) _data ??= TryLoad(_backupPath);
        if (_data is null)
        {
            // We couldn't deserialize and there's no backup or it's invalid
            // Since we are returning a new instance, the file will likely be overwritten with it later.
            // Lets make an extra backup of it in case the user wants to recover it
            BackupFile();
            _data = new();
        }

        return _data;
    }
    private T? TryLoad(string path)
    {
        _logger.LogDebug($"Attempting to load and deserialize '{path}'");
        try
        {
            using FileStream stream = File.OpenRead(path);
            return Deserialize(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogWarn(ex, $"File '{path}' not found");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, $"Failed to read '{path}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to deserialize '{path}'");
        }

        return null;
    }
    protected abstract T? Deserialize(FileStream stream);

    /// <summary>
    /// Reads the file and deserializes it.
    /// <para/>
    /// When something goes wrong, creates a new instance.
    /// </summary>
    /// <remarks>If this was already called, returns the existing instance.</remarks>
    public async ValueTask<T> TryLoadAsync()
    {
        if (_data is not null)
            return _data;

        _data = await TryLoadAsync(_filePath);
        if (_enableBackup) _data ??= await TryLoadAsync(_backupPath);
        if (_data is null)
        {
            // We couldn't deserialize and there's no backup or it's invalid
            // Since we are returning a new instance, the file will likely be overwritten with it later.
            // Lets make an extra backup of it in case the user wants to recover it
            BackupFile();
            _data = new();
        }

        return _data;
    }
    private async Task<T?> TryLoadAsync(string path)
    {
        _logger.LogDebug($"Attempting to load and deserialize '{path}'");
        try
        {
            await using FileStream stream = new(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return await DeserializeAsync(stream).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogWarn(ex, $"File '{path}' not found");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, $"Failed to read '{path}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to deserialize '{path}'");
        }

        return null;
    }
    protected abstract Task<T?> DeserializeAsync(FileStream stream);

    /// <summary>
    /// Creates a backup of the file if it exists.
    /// </summary>
    private void BackupFile()
    {
        if (File.Exists(_filePath))
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fffffff", CultureInfo.InvariantCulture);
            string originalName = Path.GetFileNameWithoutExtension(_filePath);
            string originalExt = Path.GetExtension(_filePath);
            string backupName = $"{originalName}-backup-{timestamp}{originalExt}";
            string backupPath = Path.Combine(_dirPath, backupName);

            try
            {
                File.Copy(_filePath, backupPath, false);
                _logger.LogInfo($"Backuped '{_filePath}' to '{backupPath}'");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, $"Failed to backup '{_filePath}' to '{backupPath}'");
            }
        }
    }

    /// <summary>
    /// Tries to serialize the existing instance of <typeparamref name="T"/>
    /// and save it to the file.
    /// </summary>
    /// <returns>True if the operation succeeded.</returns>
    /// <exception cref="InvalidOperationException"/>
    public bool TrySave()
    {
        _logger.LogDebug($"Attempting to serialize to '{_filePath}'");
        if (_data is null)
            throw new InvalidOperationException("Load must be called before Save()");

        try
        {
            Directory.CreateDirectory(_dirPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, $"Failed to create directory '{_dirPath}'");
            return false;
        }

        string tempPath = Path.Combine(_dirPath, $"{Guid.NewGuid()}.tmp");
        try
        {
            using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                Serialize(stream, _data);

            // Atomically replace the old file
            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, destinationBackupFileName: _enableBackup ? _backupPath : null);
            else
                File.Move(tempPath, _filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, $"Failed to write '{_filePath}'");

            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to serialize object to '{_filePath}'");
            return false;
        }

        return true;
    }
    protected abstract void Serialize(Stream stream, T data);
}

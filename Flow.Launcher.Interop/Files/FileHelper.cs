using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace Flow.Launcher.Interop.Files;

/// <summary>
/// Wrapper of <see cref="FILE_FLAGS_AND_ATTRIBUTES"/>.
/// </summary>
public enum FileAttribs
{
    ReadOnly = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_READONLY,
    Hidden = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_HIDDEN,
    System = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_SYSTEM,
    Directory = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY,
    Archive = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_ARCHIVE,
    Device = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DEVICE,
    Normal = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
    Temporary = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_TEMPORARY,
    SparseFile = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_SPARSE_FILE,
    ReparsePoint = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_REPARSE_POINT,
    Compressed = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_COMPRESSED,
    Offline = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_OFFLINE,
    NotContentIndexed = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NOT_CONTENT_INDEXED,
    Encrypted = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_ENCRYPTED,
    IntegrityStream = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_INTEGRITY_STREAM,
    Virtual = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_VIRTUAL,
    NoScrubData = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NO_SCRUB_DATA,
    Pinned = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_PINNED,
    Unpinned = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_UNPINNED,
    RecallOnOpen = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_RECALL_ON_OPEN,
    RecallOnDataAccess = (int)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
}

public static class FileHelper
{
    /// <summary>
    /// Attempts to get the attributes of the file or directory at the given path.
    /// </summary>
    /// <remarks>
    /// Use this when you need to know both the file/dir's attributes and whether it exists on a hot-path.
    /// </remarks>
    /// <param name="path">The path to the file or directory. Must be null-terminated.</param>
    /// <returns>True if the attributes are successfully retrieved (which means the path is valid).</returns>
    public static unsafe bool TryGetAttributes(ReadOnlySpan<char> path, out FileAttribs attributes)
    {
        fixed (char* pPath = path)
        {
            uint attrFlags = PInvoke.GetFileAttributes(pPath);

            if (attrFlags == PInvoke.INVALID_FILE_ATTRIBUTES)
            {
                // Path does not exist or something went wrong

                attributes = default;
                return false;
            }

            attributes = (FileAttribs)attrFlags;
            return true;
        }
    }
}

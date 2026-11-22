using System.IO;

namespace Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;

public static class DirectoryInfoSearch
{
    internal static IEnumerable<SearchResult> TopLevelDirectorySearch(
        string search, CancellationToken token, out bool isRecursive)
    {
        string criteria = ConstructSearchCriteria(search);
        int wildcardPos = search.LastIndexOf(Constants.RecursiveWildcard);

        if (wildcardPos > 0 && search[wildcardPos - 1] == Path.DirectorySeparatorChar)
        {
            isRecursive = true;
            return DirectorySearch(new EnumerationOptions
            {
                RecurseSubdirectories = true
            }, search, criteria, token);
        }

        isRecursive = false;
        return DirectorySearch(new EnumerationOptions(), search, criteria, token);
    }

    public static string ConstructSearchCriteria(string search)
    {
        string incompleteName = string.Empty;

        if (!search.EndsWith(Path.DirectorySeparatorChar))
        {
            int separatorIndex = search.LastIndexOf(Path.DirectorySeparatorChar);
            incompleteName = search[(separatorIndex + 1)..].ToLower();

            if (incompleteName.StartsWith(Constants.RecursiveWildcard))
                incompleteName = string.Concat("*", incompleteName.AsSpan(1));
        }

        incompleteName += "*";
        return incompleteName;
    }

    private static IEnumerable<SearchResult> DirectorySearch(
        EnumerationOptions enumerationOption, string search,
        string searchCriteria, CancellationToken token)
    {
        string path = Path.GetDirectoryName(search) ?? search;
        System.IO.DirectoryInfo dirInfo = new(path);

        if (!enumerationOption.RecurseSubdirectories)
        {
            foreach (SearchResult result in EnumerateFileSystemEntries(
                dirInfo.EnumerateDirectories(searchCriteria, enumerationOption), path, token))
            {
                yield return result;
            }

            foreach (SearchResult result in EnumerateFileSystemEntries(
                dirInfo.EnumerateFiles(searchCriteria, enumerationOption), path, token))
            {
                yield return result;
            }

            yield break;
        }

        foreach (SearchResult result in EnumerateFileSystemEntries(
            dirInfo.EnumerateFileSystemInfos(searchCriteria, enumerationOption), path, token))
        {
            yield return result;
        }
    }

    private static IEnumerable<SearchResult> EnumerateFileSystemEntries(
        IEnumerable<FileSystemInfo> entries, string path, CancellationToken token)
    {
        IEnumerator<FileSystemInfo> enumerator;
        try
        {
            enumerator = entries.GetEnumerator();
        }
        catch (Exception e)
        {
            Main.Context.Logger.LogError(e, $"Failed to search in path '{path}'");
            throw;
        }

        using (enumerator)
        {
            while (true)
            {
                FileSystemInfo fileSystemInfo;
                try
                {
                    if (!enumerator.MoveNext())
                        yield break;

                    fileSystemInfo = enumerator.Current;
                }
                catch (Exception e)
                {
                    Main.Context.Logger.LogError(e, $"Failed to search in path '{path}'");
                    throw;
                }

                token.ThrowIfCancellationRequested();

                yield return new SearchResult
                {
                    FullPath = fileSystemInfo.FullName,
                    Type = fileSystemInfo switch
                    {
                        System.IO.DirectoryInfo { Parent: null } => ResultType.Volume,
                        System.IO.DirectoryInfo => ResultType.Folder,
                        FileInfo => ResultType.File,
                        _ => throw new InvalidOperationException(
                            $"Unexpected FileSystemInfo type: {fileSystemInfo.GetType()}"),
                    },
                };
            }
        }
    }
}

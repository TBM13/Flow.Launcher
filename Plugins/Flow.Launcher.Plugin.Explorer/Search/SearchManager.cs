using System.IO;
using System.Runtime.CompilerServices;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;

namespace Flow.Launcher.Plugin.Explorer.Search;

public class SearchManager(Settings settings, PluginInitContext context)
{
    internal PluginInitContext Context = context;
    internal Settings Settings = settings;

    /// <summary>
    /// Note: A path that ends with "\" and one that doesn't will not be regarded as equal.
    /// </summary>
    public class PathEqualityComparator : IEqualityComparer<Result>
    {
        public static PathEqualityComparator Default => field ??= new PathEqualityComparator();

        public bool Equals(Result? x, Result? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return x.Title.Equals(y.Title, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.SubTitle, y.SubTitle, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Result obj)
        {
            return HashCode.Combine(obj.Title.ToLowerInvariant(), obj.SubTitle?.ToLowerInvariant() ?? "");
        }
    }

    internal async IAsyncEnumerable<Result> SearchAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        bool isPathSearch = Path.IsPathFullyQualified(query.Search)
            || EnvironmentVariables.IsEnvironmentVariableSearch(query.Search)
            || EnvironmentVariables.HasEnvironmentVar(query.Search);

        if (!isPathSearch)
            yield break;

        await foreach (Result result in PathSearchAsync(query, token).ConfigureAwait(false))
            yield return result;
    }

    private async IAsyncEnumerable<Result> PathSearchAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        string querySearch = query.Search;

        if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch))
        {
            foreach (Result suggestion in EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, Context))
            {
                token.ThrowIfCancellationRequested();
                yield return suggestion;
            }
            yield break;
        }

        // Query is a location path with a full environment variable, eg. %appdata%\somefolder\, c:\users\%USERNAME%\downloads
        bool needToExpand = EnvironmentVariables.HasEnvironmentVar(querySearch);
        string path = needToExpand ? Environment.ExpandEnvironmentVariables(querySearch) : querySearch;

        // if user uses the unix directory separator, we need to convert it to windows directory separator
        path = path.Replace(Constants.UnixDirectorySeparator, Path.DirectorySeparatorChar);

        // Check that actual location exists, otherwise directory search will throw directory not found exception
        string dirPath = Path.GetDirectoryName(path) ?? path;
        if (!Directory.Exists(dirPath))
            yield break;

        HashSet<Result> seenResults = new(PathEqualityComparator.Default);

        if (path.EndsWith('\\'))
        {
            Result headerResult = path.EndsWith(":\\")
                ? ResultManager.CreateDriveSpaceDisplayResult(path)
                : ResultManager.CreateOpenCurrentFolderResult(path);

            if (seenResults.Add(headerResult))
                yield return headerResult;
        }

        token.ThrowIfCancellationRequested();

        IEnumerable<SearchResult> directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(path, token, out bool isRecursive);
        bool isSearch = !path.EndsWith('\\');

        foreach (SearchResult directory in directoryResult)
        {
            token.ThrowIfCancellationRequested();

            Result result = ResultManager.CreateResult(directory, isRecursive, isSearch);
            if (seenResults.Add(result))
                yield return result;
        }
    }
}

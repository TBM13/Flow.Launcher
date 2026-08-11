using System.IO;
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

    internal async Task<List<Result>> SearchAsync(Query query, CancellationToken token)
    {
        bool isPathSearch = Path.IsPathFullyQualified(query.Search)
            || EnvironmentVariables.IsEnvironmentVariableSearch(query.Search)
            || EnvironmentVariables.HasEnvironmentVar(query.Search);

        if (isPathSearch)
            return await PathSearchAsync(query, token).ConfigureAwait(false);

        return [];
    }

    private async Task<List<Result>> PathSearchAsync(Query query, CancellationToken token = default)
    {
        var querySearch = query.Search;
        var results = new HashSet<Result>(PathEqualityComparator.Default);

        if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch))
            return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, Context);

        // Query is a location path with a full environment variable, eg. %appdata%\somefolder\, c:\users\%USERNAME%\downloads
        var needToExpand = EnvironmentVariables.HasEnvironmentVar(querySearch);
        var path = needToExpand ? Environment.ExpandEnvironmentVariables(querySearch) : querySearch;

        // if user uses the unix directory separator, we need to convert it to windows directory separator
        path = path.Replace(Constants.UnixDirectorySeparator, Path.DirectorySeparatorChar);

        // Check that actual location exists, otherwise directory search will throw directory not found exception
        string dirPath = Path.GetDirectoryName(path) ?? path;
        if (!Directory.Exists(dirPath))
            return [.. results];

        if (path.EndsWith('\\'))
        {
            results.Add(path.EndsWith(":\\")
                ? ResultManager.CreateDriveSpaceDisplayResult(path)
                : ResultManager.CreateOpenCurrentFolderResult(path));
        }

        if (token.IsCancellationRequested)
            return [];

        IAsyncEnumerable<SearchResult> directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(path, token, out bool isRecursive).ToAsyncEnumerable();

        if (token.IsCancellationRequested)
            return [];

        await foreach (var directory in directoryResult.WithCancellation(token).ConfigureAwait(false))
        {
            results.Add(ResultManager.CreateResult(directory, isRecursive));
        }

        return [.. results];
    }
}

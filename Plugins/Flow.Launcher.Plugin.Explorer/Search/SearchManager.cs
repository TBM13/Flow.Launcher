using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        internal PluginInitContext Context;

        internal Settings Settings;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            Context = context;
            Settings = settings;
        }

        /// <summary>
        /// Note: A path that ends with "\" and one that doesn't will not be regarded as equal.
        /// </summary>
        public class PathEqualityComparator : IEqualityComparer<Result>
        {
            private static PathEqualityComparator instance;
            public static PathEqualityComparator Instance => instance ??= new PathEqualityComparator();

            public bool Equals(Result x, Result y)
            {
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
            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch))
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, Context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\, c:\users\%USERNAME%\downloads
            var needToExpand = EnvironmentVariables.HasEnvironmentVar(querySearch);
            var path = needToExpand ? Environment.ExpandEnvironmentVariables(querySearch) : querySearch;

            // if user uses the unix directory separator, we need to convert it to windows directory separator
            path = path.Replace(Constants.UnixDirectorySeparator, Constants.DirectorySeparator);

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            string dirPath = Path.GetDirectoryName(path) ?? path;
            if (!Directory.Exists(dirPath))
                return [.. results];

            if (path.EndsWith('\\'))
            {
                results.Add(path.EndsWith(":\\")
                    ? ResultManager.CreateDriveSpaceDisplayResult(query, path)
                    : ResultManager.CreateOpenCurrentFolderResult(query, path));
            }

            if (token.IsCancellationRequested)
                return [];

            IAsyncEnumerable<SearchResult> directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(query, path, token, out bool isRecursive).ToAsyncEnumerable();

            if (token.IsCancellationRequested)
                return [];

            try
            {
                await foreach (var directory in directoryResult.WithCancellation(token).ConfigureAwait(false))
                {
                    results.Add(ResultManager.CreateResult(query, directory, isRecursive));
                }
            }
            catch (Exception e)
            {
                throw new SearchException(e.Message, e);
            }


            return [.. results];
        }
    }
}

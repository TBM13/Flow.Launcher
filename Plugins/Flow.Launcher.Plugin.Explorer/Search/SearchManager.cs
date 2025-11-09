using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.SharedCommands;
using Path = System.IO.Path;

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
            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            // This allows the user to type the below action keywords and see/search the list of quick folder links
            if (ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword))
            {
                if (string.IsNullOrEmpty(query.Search) && ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword))
                    return QuickAccess.AccessLinkListAll(query, Settings.QuickAccessLinks);
            }
            else
            {
                // No action keyword matched- plugin should not handle this query, return empty results.
                return new List<Result>();
            }

            bool isPathSearch = query.Search.IsLocationPathString()
                || EnvironmentVariables.IsEnvironmentVariableSearch(query.Search)
                || EnvironmentVariables.HasEnvironmentVar(query.Search);

            switch (isPathSearch)
            {
                case true
                    when ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword)
                         || ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword):

                    results.UnionWith(await PathSearchAsync(query, token).ConfigureAwait(false));

                    return results.ToList();

                case true or false
                    when ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword):
                    return QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks);

                default:
                    return results.ToList();
            }
        }

        private bool ActionKeywordMatch(Query query, Settings.ActionKeyword allowedActionKeyword)
        {
            var keyword = query.ActionKeyword.Length == 0 ? Query.GlobalPluginWildcardSign : query.ActionKeyword;

            return allowedActionKeyword switch
            {
                Settings.ActionKeyword.SearchActionKeyword => Settings.SearchActionKeywordEnabled &&
                                                              keyword == Settings.SearchActionKeyword,
                Settings.ActionKeyword.PathSearchActionKeyword => Settings.PathSearchKeywordEnabled &&
                                                                  keyword == Settings.PathSearchActionKeyword,
                Settings.ActionKeyword.QuickAccessActionKeyword => Settings.QuickAccessKeywordEnabled &&
                                                                   keyword == Settings.QuickAccessActionKeyword,
                _ => throw new ArgumentOutOfRangeException(nameof(allowedActionKeyword), allowedActionKeyword, "actionKeyword out of range")
            };
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
            if (!FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path).LocationExists())
                return results.ToList();

            var retrievedDirectoryPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path);

            results.Add(retrievedDirectoryPath.EndsWith(":\\")
                ? ResultManager.CreateDriveSpaceDisplayResult(retrievedDirectoryPath, query.ActionKeyword)
                : ResultManager.CreateOpenCurrentFolderResult(retrievedDirectoryPath, query.ActionKeyword));

            if (token.IsCancellationRequested)
                return new List<Result>();

            IAsyncEnumerable<SearchResult> directoryResult;

            var recursiveIndicatorIndex = path.IndexOf('>');

            directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(query, path, token).ToAsyncEnumerable();

            if (token.IsCancellationRequested)
                return new List<Result>();

            try
            {
                await foreach (var directory in directoryResult.WithCancellation(token).ConfigureAwait(false))
                {
                    results.Add(ResultManager.CreateResult(query, directory));
                }
            }
            catch (Exception e)
            {
                throw new SearchException(e.Message, e);
            }


            return results.ToList();
        }

        private bool IsExcludedFile(SearchResult result)
        {
            string[] excludedFileTypes = Settings.ExcludedFileTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string fileExtension = Path.GetExtension(result.FullPath).TrimStart('.');

            return excludedFileTypes.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }
    }
}

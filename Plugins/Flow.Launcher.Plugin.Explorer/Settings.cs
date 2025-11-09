using System;
using System.Collections.ObjectModel;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Settings
    {
        public int MaxResult { get; set; } = 100;

        public ObservableCollection<AccessLink> QuickAccessLinks { get; set; } = [];

        public string EditorPath { get; set; } = "";

        public string FolderEditorPath { get; set; } = "";

        public string ShellPath { get; set; } = "cmd";

        public string ExcludedFileTypes { get; set; } = "";

        public bool UseLocationAsWorkingDir { get; set; } = false;

        public bool ShowInlinedWindowsContextMenu { get; set; } = false;

        public string WindowsContextMenuIncludedItems { get; set; } = string.Empty;

        public string WindowsContextMenuExcludedItems { get; set; } = string.Empty;

        public bool DefaultOpenFolderInFileManager { get; set; } = false;

        public bool DisplayMoreInformationInToolTip { get; set; } = false;

        public string SearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool SearchActionKeywordEnabled { get; set; } = true;

        public string PathSearchActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool PathSearchKeywordEnabled { get; set; }

        public string QuickAccessActionKeyword { get; set; } = Query.GlobalPluginWildcardSign;

        public bool QuickAccessKeywordEnabled { get; set; }

        public bool ShowFileSizeInPreviewPanel { get; set; } = true;

        public bool ShowCreatedDateInPreviewPanel { get; set; } = true;

        public bool ShowModifiedDateInPreviewPanel { get; set; } = true;

        public bool ShowFileAgeInPreviewPanel { get; set; } = false;

        public string PreviewPanelDateFormat { get; set; } = "yyyy-MM-dd";

        public string PreviewPanelTimeFormat { get; set; } = "HH:mm";

        internal enum ActionKeyword
        {
            SearchActionKeyword,
            PathSearchActionKeyword,
            QuickAccessActionKeyword
        }

        internal string GetActionKeyword(ActionKeyword actionKeyword) => actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeyword,
            ActionKeyword.PathSearchActionKeyword => PathSearchActionKeyword,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessActionKeyword,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyWord property not found")
        };

        internal void SetActionKeyword(ActionKeyword actionKeyword, string keyword) => _ = actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeyword = keyword,
            ActionKeyword.PathSearchActionKeyword => PathSearchActionKeyword = keyword,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessActionKeyword = keyword,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyWord property not found")
        };

        internal bool GetActionKeywordEnabled(ActionKeyword actionKeyword) => actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeywordEnabled,
            ActionKeyword.PathSearchActionKeyword => PathSearchKeywordEnabled,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessKeywordEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyword enabled status not defined")
        };

        internal void SetActionKeywordEnabled(ActionKeyword actionKeyword, bool enable) => _ = actionKeyword switch
        {
            ActionKeyword.SearchActionKeyword => SearchActionKeywordEnabled = enable,
            ActionKeyword.PathSearchActionKeyword => PathSearchKeywordEnabled = enable,
            ActionKeyword.QuickAccessActionKeyword => QuickAccessKeywordEnabled = enable,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKeyword), actionKeyword, "ActionKeyword enabled status not defined")
        };
    }
}

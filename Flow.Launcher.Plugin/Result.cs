using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Describes a result of a <see cref="Query"/> executed by a plugin
    /// </summary>
    public record Result
    {
        /// <summary>
        /// Maximum score. This can be useful when set one result to the top by default. This is the score for the results set to the topmost by users.
        /// </summary>
        public const int MaxScore = int.MaxValue;

        /// <summary>
        /// The title of the result. This is always required.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Provides additional details for the result. This is optional
        /// </summary>
        public string SubTitle { get; set; } = string.Empty;

        /// <summary>
        /// Holds the action keyword that triggered the result.
        /// If result is triggered by global keyword: *, this should be null.
        /// </summary>
        public string? ActionKeywordAssigned { get; set; }

        /// <summary>
        /// This holds the text which can be provided by plugin to be copied to the
        /// user's clipboard when Ctrl + C is pressed on a result. If the text is a file/directory path
        /// flow will copy the actual file/folder instead of just the path text.
        /// </summary>
        public string CopyText
        {
            get => string.IsNullOrEmpty(field) ? SubTitle : field;
            set => field = value;
        }

        /// <summary>
        /// This holds the text which can be provided by plugin to help Flow autocomplete text
        /// for user on the plugin result. If autocomplete action for example is tab, pressing tab will have
        /// the default constructed autocomplete text (result's Title), or the text provided here if not empty.
        /// </summary>
        /// <remarks>
        /// When a value is not set, the <see cref="Title"/> will be used.
        /// Please include the action keyword prefix when necessary because Flow does not prepend it automatically.
        /// </remarks>
        public string? AutoCompleteText { get; set; }

        /// <summary>
        /// The image to be displayed for the result.
        /// </summary>
        /// <value>Can be a local file path or a URL.</value>
        /// <remarks>GlyphInfo is prioritized if not null</remarks>
        public string IcoPath
        {
            get => field;
            set
            {
                // As a standard this property will handle prepping and converting to absolute local path for icon image processing
                if (!string.IsNullOrEmpty(value)
                    && !string.IsNullOrEmpty(PluginDirectory)
                    && !Path.IsPathRooted(value)
                    && !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    && !value.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    field = Path.Combine(PluginDirectory, value);
                }
                else
                {
                    field = value;
                }
            }
        }

        /// <summary>
        /// Delegate function that produces an <see cref="ImageSource"/>
        /// </summary>
        /// <returns></returns>
        public delegate ImageSource IconDelegate();

        /// <summary>
        /// Delegate to load an icon for this result.
        /// </summary>
        public IconDelegate? Icon = null;

        /// <summary>
        /// Information for Glyph Icon (Prioritized than IcoPath/Icon if user enable Glyph Icons)
        /// </summary>
        public GlyphInfo? Glyph { get; set; }

        /// <summary>
        /// An action to take in the form of a function call when the result has been selected.
        /// </summary>
        /// <remarks>
        /// The function is invoked with an <see cref="ActionContext"/> as the only parameter.
        /// Its result determines what happens to Flow Launcher's query form:
        /// when true, the form will be hidden; when false, it will stay in focus.
        /// </remarks>
        public Func<ActionContext, bool>? Action { get; set; }

        /// <summary>
        /// An async action to take in the form of a function call when the result has been selected.
        /// </summary>
        /// <remarks>
        /// The function is invoked with an <see cref="ActionContext"/> as the only parameter and awaited.
        /// Its result determines what happens to Flow Launcher's query form:
        /// when true, the form will be hidden; when false, it will stay in focus.
        /// </remarks>
        public Func<ActionContext, ValueTask<bool>>? AsyncAction { get; set; }

        /// <summary>
        /// Priority of the current result
        /// </summary>
        /// <value>default: 0</value>
        public int Score { get; set; }

        /// <summary>
        /// Query information associated with the result
        /// </summary>
        internal Query? OriginQuery { get; set; }

        /// <summary>
        /// Plugin directory
        /// </summary>
        public string? PluginDirectory
        {
            get => field;
            set
            {
                field = value;

                // When the Result object is returned from the query call, PluginDirectory is not provided until
                // UpdatePluginMetadata call is made at PluginManager.cs L196. Once the PluginDirectory becomes available
                // we need to update (only if not Uri path) the IcoPath with the full absolute path so the image can be loaded.
#pragma warning disable CA2245
                IcoPath = IcoPath;
#pragma warning restore CA2245
            }
        }

        /// <summary>
        /// Additional data associated with this result
        /// </summary>
        /// <example>
        /// As external information for ContextMenu
        /// </example>
        public object? ContextData { get; set; }

        /// <summary>
        /// Plugin ID that generated this result
        /// </summary>
        public string? PluginID { get; internal set; }

        /// <summary>
        /// Show message as ToolTip on result Title hover over
        /// </summary>
        public string? TitleToolTip { get; set; }

        /// <summary>
        /// Show message as ToolTip on result SubTitle hover over
        /// </summary>
        public string? SubTitleToolTip { get; set; }

        /// <summary>
        /// Customized Preview Panel
        /// </summary>
        public Lazy<UserControl>? PreviewPanel { get; set; }

        /// <summary>
        /// Contains data used to populate the preview section of this result.
        /// </summary>
        public PreviewInfo Preview { get; set; } = PreviewInfo.Default;

        /// <summary>
        /// Determines if the user selection count should be added to the score. This can be useful when set to false to allow the result sequence order to be the same everytime instead of changing based on selection.
        /// </summary>
        public bool AddSelectedCount { get; set; } = true;

        /// <summary>
        /// The key to identify the record. This is used when FL checks whether the result is the topmost record. Or FL calculates the hashcode of the result for user selected records.
        /// This can be useful when your plugin will change the Title or SubTitle of the result dynamically.
        /// If the plugin does not specific this, FL just uses Title and SubTitle to identify this result.
        /// Note: Because old data does not have this key, we should use null as the default value for consistency.
        /// </summary>
        public string? RecordKey { get; set; } = null;

        /// <summary>
        /// This holds the text which can be shown as a query suggestion.
        /// </summary>
        /// <remarks>
        /// When a value is not set, the <see cref="Title"/> will be used.
        /// Do not include the action keyword prefix because Flow prepends it automatically.
        /// If the it does not start with the query text, it will not be shown as a suggestion.
        /// So make sure to set this value to start with the query text.
        /// </remarks>
        public string? QuerySuggestionText { get; set; }

        /// <summary>
        /// Run this result, asynchronously
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public ValueTask<bool> ExecuteAsync(ActionContext context)
        {
            return AsyncAction?.Invoke(context) ?? ValueTask.FromResult(Action?.Invoke(context) ?? false);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Title + SubTitle + Score;
        }

        /// <summary>
        /// Info of the preview section of a <see cref="Result"/>
        /// </summary>
        public record PreviewInfo
        {
            /// <summary>
            /// Full image used for preview panel
            /// </summary>
            public string? PreviewImagePath { get; set; } = null;

            /// <summary>
            /// Determines if the preview image should occupy the full width of the preview panel.
            /// </summary>
            public bool IsMedia { get; set; } = false;

            /// <summary>
            /// Result description text that is shown at the bottom of the preview panel.
            /// </summary>
            /// <remarks>
            /// When a value is not set, the <see cref="SubTitle"/> will be used.
            /// </remarks>
            public string? Description { get; set; } = null;

            /// <summary>
            /// Delegate to get the preview panel's image
            /// </summary>
            public IconDelegate? PreviewDelegate { get; set; } = null;

            /// <summary>
            /// File path of the result. For third-party programs providing external preview.
            /// </summary>
            public string? FilePath { get; set; } = null;

            /// <summary>
            /// Default instance of <see cref="PreviewInfo"/>
            /// </summary>
            public static PreviewInfo Default { get; } = new()
            {
                PreviewImagePath = null,
                Description = null,
                IsMedia = false,
                PreviewDelegate = null,
                FilePath = null,
            };
        }
    }
}

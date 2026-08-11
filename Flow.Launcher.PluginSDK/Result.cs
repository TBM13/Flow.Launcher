using System.Windows.Controls;

namespace Flow.Launcher.PluginSDK;

/// <summary>
/// Describes a result of a <see cref="Query"/>.
/// </summary>
public record Result
{
    /// <summary>
    /// The maximum score a result can have.
    /// <para/>
    /// Useful to make a result appear at the top of the list.
    /// </summary>
    public const int MaxScore = int.MaxValue;

    /// <summary>
    /// The title of the result.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Provides additional details for the result. This is optional
    /// </summary>
    public string SubTitle { get; set; } = string.Empty;

    /// <summary>
    /// The text that will be copied to the user's clipboard when
    /// Ctrl + C is pressed on this result.
    /// <para/>
    /// If this is the path of a file/directory, flow will copy the actual file/folder.
    /// </summary>
    public string CopyText
    {
        get => string.IsNullOrEmpty(field) ? SubTitle : field;
        set => field = value;
    }

    /// <summary>
    /// If provided, the query will be replaced with the value of this property when
    /// the user presses TAB (or whatever the autocomplete hotkey is) on this result.
    /// <para/>
    /// Please include the action keyword prefix when necessary because we don't prepend it automatically.
    /// </summary>
    public string? AutoCompleteText { get; set; }

    /// <summary>
    /// The image to be displayed for the result.
    /// </summary>
    /// <remarks><see cref="Glyph"/> is prioritized if it's not null.</remarks>
    public string? IcoPath { get; set; }

    /// <summary>
    /// A glyph that will be displayed with the result, instead of an icon image.
    /// </summary>
    public string? Glyph { get; set; }

    /// <summary>
    /// The action that will be executed when the result is selected.
    /// </summary>
    /// <remarks>
    /// If the result of the function is true, Flow Launcher's window will be hidden.
    /// </remarks>
    public Func<ActionContext, bool>? Action { get; set; }

    /// <summary>
    /// The async action that will be executed when the result is selected.
    /// </summary>
    /// <remarks>
    /// If the result of the function is true, Flow Launcher's window will be hidden.
    /// </remarks>
    public Func<ActionContext, ValueTask<bool>>? AsyncAction { get; set; }

    /// <summary>
    /// Priority of the current result.
    /// </summary>
    /// <value>default: 0</value>
    public int Score { get; set; }

    /// <summary>
    /// Additional data associated with this result
    /// </summary>
    /// <example>
    /// As external information for ContextMenu
    /// </example>
    public object? ContextData { get; set; }

    /// <summary>
    /// The ID of the plugin that generated this result.
    /// </summary>
    public string? PluginID { get; internal set; }

    /// <summary>
    /// Tooltip that should be shown when the user hovers over the result.
    /// <para/>
    /// If this is not set, <see cref="Title"/> and <see cref="SubTitle"/> will be shown instead.
    /// </summary>
    public string? ToolTip { get; set; }

    /// <summary>
    /// Customized Preview Panel
    /// </summary>
    public Lazy<UserControl>? PreviewPanel { get; set; }

    /// <summary>
    /// Contains data used to populate the preview section of this result.
    /// </summary>
    public PreviewInfo Preview { get; set; } = new();

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
        /// Result description text that is shown at the bottom of the preview panel.
        /// </summary>
        /// <remarks>
        /// When a value is not set, the <see cref="SubTitle"/> will be used.
        /// </remarks>
        public string? Description { get; set; } = null;
    }
}

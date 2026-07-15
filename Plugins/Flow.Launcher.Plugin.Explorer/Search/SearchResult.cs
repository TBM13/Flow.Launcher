namespace Flow.Launcher.Plugin.Explorer.Search;

public enum ResultType
{
    Volume,
    Folder,
    File
}

public readonly record struct SearchResult
{
    public string FullPath { get; init; }
    public ResultType Type { get; init; }
    public int Score { get; init; }
}

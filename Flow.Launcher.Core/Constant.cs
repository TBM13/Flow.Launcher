using System.Diagnostics;
using System.Reflection;

namespace Flow.Launcher.Core;

public static class Constant
{
    public const string FlowLauncher = "Flow.Launcher";
    public const string Plugins = "Plugins";

    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
    public static readonly string ProgramDirectory = AppContext.BaseDirectory;

    public const string IssuesUrl = "https://github.com/TBM13/Flow.Launcher/issues";
    public static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion ?? "<Unknown>";
    public static readonly string Dev = "Dev";

    private static readonly string ImagesDirectory = Path.Combine(ProgramDirectory, "Images");
    public static readonly string DefaultIcon = Path.Combine(ImagesDirectory, "app.png");
    public static readonly string ErrorIcon = Path.Combine(ImagesDirectory, "app_error.png");
    public static readonly string MissingImgIcon = Path.Combine(ImagesDirectory, "app_missing_img.png");
    public static readonly string LoadingImgIcon = Path.Combine(ImagesDirectory, "loading.png");
    public static readonly string ImageIcon = Path.Combine(ImagesDirectory, "image.png");

    public const string Themes = "Themes";
    public const string Settings = "Settings";
    public const string Cache = "Cache";
}

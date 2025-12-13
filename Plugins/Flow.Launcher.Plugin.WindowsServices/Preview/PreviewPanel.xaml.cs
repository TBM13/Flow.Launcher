using System.Windows.Controls;

namespace Flow.Launcher.Plugin.WindowsServices.Preview;

public partial class PreviewPanel : UserControl
{
    public PreviewPanel(ServiceResult svcResult)
    {
        InitializeComponent();

        TitleTextblock.Text = svcResult.DisplayName;
        DescriptionTextblock.Text = svcResult.GetDescription() ?? Localize.plugin_windowsservices_error_getDescriptionFail();
        PathTextblock.Text = svcResult.GetImagePath() ?? Localize.plugin_windowsservices_error_getPathFail();
        PathTextblock.ToolTip = PathTextblock.Text;
    }
}

using System.Windows.Controls;

namespace Flow.Launcher.Plugin.WindowsServices.Preview;

public partial class PreviewPanel : UserControl
{
    public PreviewPanel(ServiceResult svcResult)
    {
        InitializeComponent();

        TitleTextblock.Text = svcResult.DisplayName;
        DescriptionTextblock.Text = svcResult.GetDescription() ?? "<Failed to get description>";
        PathTextblock.Text = svcResult.GetImagePath() ?? "<Failed to get path>";
        PathTextblock.ToolTip = PathTextblock.Text;
    }
}

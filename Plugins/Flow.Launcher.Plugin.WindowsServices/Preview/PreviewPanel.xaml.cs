using System.Windows.Controls;

namespace Flow.Launcher.Plugin.WindowsServices.Preview;

public partial class PreviewPanel : UserControl
{
    public PreviewPanel(ServiceResult svcResult)
    {
        InitializeComponent();

        TitleTextblock.Text = svcResult.DisplayName;
        DescriptionTextblock.Text = svcResult.GetDescription() ?? Localize.Error_GetDescriptionFail;
        PathTextblock.Text = svcResult.GetImagePath() ?? Localize.Error_GetPathFail;
        PathTextblock.ToolTip = PathTextblock.Text;
    }
}

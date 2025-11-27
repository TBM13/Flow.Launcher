using System.Windows.Controls;

namespace Flow.Launcher.Plugin.WindowsServices.Preview;

public partial class PreviewPanel : UserControl
{
    public PreviewPanel(ServiceResult svcResult)
    {
        InitializeComponent();

        TitleTextblock.Text = svcResult.DisplayName;
        DescriptionTextblock.Text = svcResult.GetDescription();
    }
}

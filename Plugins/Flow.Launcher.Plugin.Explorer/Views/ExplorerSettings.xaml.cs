using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.ViewModels;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    public partial class ExplorerSettings
    {
        private readonly List<Expander> _expanders;

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
            DataContext = viewModel;

            _expanders =
            [
                GeneralSettingsExpander,
                PreviewPanelExpander
            ];
        }

        private void AllowOnlyNumericInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = e.Text.ToCharArray().Any(c => !char.IsDigit(c));
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander expandedExpander)
            {
                // Ensure _expanders is not null and contains items
                if (_expanders == null || _expanders.Count == 0) return;

                foreach (var expander in _expanders)
                {
                    if (expander != null && expander != expandedExpander && expander.IsExpanded)
                    {
                        expander.IsExpanded = false;
                    }
                }
            }
        }
    }
}

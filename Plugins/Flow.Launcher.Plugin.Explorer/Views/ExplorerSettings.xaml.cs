using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Flow.Launcher.Plugin.Explorer.Views
{
    public partial class ExplorerSettings
    {
        private readonly SettingsViewModel _viewModel;
        private readonly List<Expander> _expanders;

        public ExplorerSettings(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            InitializeComponent();

            DataContext = viewModel;

            _expanders =
            [
                GeneralSettingsExpander,
                ContextMenuExpander,
                PreviewPanelExpander
            ];
        }

        private void lbxAccessLinks_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
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

        private void lbxAccessLinks_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not ListView listView) return;
            if (listView.View is not GridView gView) return;

            var workingWidth =
                listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar

            if (workingWidth <= 0) return;

            var col1 = 0.4;
            var col2 = 0.6;

            gView.Columns[0].Width = workingWidth * col1;
            gView.Columns[1].Width = workingWidth * col2;
        }
    }
}

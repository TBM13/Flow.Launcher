using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ActionKeywords
    {
        private readonly PluginMetadata _plugin;
        private readonly PluginViewModel _pluginViewModel;

        public ActionKeywords(PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            _plugin = pluginViewModel.PluginMetadata;
            _pluginViewModel = pluginViewModel;
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.TermSeparator, _plugin.ActionKeywords);
            tbAction.Text = tbOldActionKeyword.Text;
            tbAction.SelectAll();
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs _)
        {
            var oldActionKeywords = _plugin.ActionKeywords;

            var newActionKeywords = tbAction.Text.Split(Query.TermSeparator)
                                                 .Where(s => !string.IsNullOrEmpty(s))
                                                 .Distinct()
                                                 .ToList();

            newActionKeywords = newActionKeywords.Count > 0 ? newActionKeywords : new() { Query.GlobalPluginWildcard };

            var addedActionKeywords = newActionKeywords.Except(oldActionKeywords).ToList();
            var removedActionKeywords = oldActionKeywords.Except(newActionKeywords).ToList();

            if (addedActionKeywords.Any(IPublicAPI.Instance.ActionKeywordAssigned))
            {
                MessageBox.Show("This new Action Keyword is already assigned to another plugin, please choose a different one");
                return;
            }

            if (oldActionKeywords.Count != newActionKeywords.Count)
            {
                ReplaceActionKeyword(_plugin.ID, removedActionKeywords, addedActionKeywords);
                return;
            }

            var sortedOldActionKeywords = oldActionKeywords.OrderBy(s => s).ToList();
            var sortedNewActionKeywords = newActionKeywords.OrderBy(s => s).ToList();

            if (sortedOldActionKeywords.SequenceEqual(sortedNewActionKeywords))
            {
                // User just changes the sequence of action keywords
                MessageBox.Show("This new Action Keyword is the same as old, please choose a different one");
            }
            else
            {
                ReplaceActionKeyword(_plugin.ID, removedActionKeywords, addedActionKeywords);
            }
        }

        private void ReplaceActionKeyword(string id, IReadOnlyList<string> removedActionKeywords, IReadOnlyList<string> addedActionKeywords)
        {
            foreach (var actionKeyword in removedActionKeywords)
            {
                IPublicAPI.Instance.RemoveActionKeyword(id, actionKeyword);
            }
            foreach (var actionKeyword in addedActionKeywords)
            {
                IPublicAPI.Instance.AddActionKeyword(id, actionKeyword);
            }

            // Update action keywords text and close window
            _pluginViewModel.OnActionKeywordsTextChanged();
            Close();
        }
    }
}

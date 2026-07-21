using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Core.Settings;

namespace Flow.Launcher.Core.Resource
{
    public partial class Theme(ISettingsAPI settings) : ObservableObject
    {
        private static readonly string DefaultThemePath = $@"{Constant.ProgramDirectory}\{Constant.Themes}\Base.xaml";

        private readonly ISettingsAPI _settings = settings;
        private ResourceDictionary? _oldResource;

        /// <summary>
        /// The ResizeBorderThickness of the current theme.
        /// </summary>
        [ObservableProperty]
        public partial Thickness ThemeResizeBorderThickness { get; private set; }

        private void UpdateResourceDictionary(ResourceDictionary dictionaryToUpdate)
        {
            // Add new resources
            if (!Application.Current.Resources.MergedDictionaries.Contains(dictionaryToUpdate))
            {
                Application.Current.Resources.MergedDictionaries.Add(dictionaryToUpdate);
            }

            // Remove old resources
            if (_oldResource != null && _oldResource != dictionaryToUpdate &&
                Application.Current.Resources.MergedDictionaries.Contains(_oldResource))
            {
                Application.Current.Resources.MergedDictionaries.Remove(_oldResource);
            }

            _oldResource = dictionaryToUpdate;
        }

        private ResourceDictionary GetResourceDictionary()
        {
            if (!File.Exists(DefaultThemePath))
                throw new FileNotFoundException($"Theme can't be found <{DefaultThemePath}>");

            var dict = new ResourceDictionary
            {
                Source = new Uri(DefaultThemePath, UriKind.Absolute)
            };

            /* Ignore Theme Window Width and use setting */
            Style windowStyle = dict["WindowStyle"] as Style ?? throw new NullReferenceException("WindowStyle not found in resource dictionary.");
            double width = _settings.WindowWidth;
            windowStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return dict;
        }

        public bool ChangeTheme()
        {
            // Retrieve theme resource – always use the resource with font settings applied.
            var resourceDict = GetResourceDictionary();
            UpdateResourceDictionary(resourceDict);

            return true;
        }
    }
}

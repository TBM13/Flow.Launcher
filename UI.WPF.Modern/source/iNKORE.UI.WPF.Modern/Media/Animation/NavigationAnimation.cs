using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using iNKORE.UI.WPF.Modern.Common;

namespace iNKORE.UI.WPF.Modern.Media.Animation
{
    internal class NavigationAnimation
    {
        static NavigationAnimation()
        {
            _defaultBitmapCache = new BitmapCache();
            _defaultBitmapCache.Freeze();
        }

        public NavigationAnimation(FrameworkElement element, Storyboard storyboard)
        {
            _element = element;
            _storyboard = storyboard;
            _storyboard.CurrentStateInvalidated += OnCurrentStateInvalidated;
            _storyboard.Completed += OnCompleted;
        }

        public event EventHandler Completed;

        public void Begin()
        {
            if (ShadowAssist.UseBitmapCache && _element.CacheMode is not BitmapCache)
            {
                _element.SetCurrentValue(UIElement.CacheModeProperty, GetBitmapCache());
            }
            _storyboard.Begin(_element, true);
        }

        public void Stop()
        {
            if (_currentState != ClockState.Stopped)
            {
                _storyboard.Stop(_element);
            }
            if (ShadowAssist.UseBitmapCache)
            {
                _element.InvalidateProperty(UIElement.CacheModeProperty);
            }
            _element.InvalidateProperty(UIElement.RenderTransformProperty);
            _element.InvalidateProperty(UIElement.RenderTransformOriginProperty);
        }

        private void OnCurrentStateInvalidated(object sender, EventArgs e)
        {
            if (sender is Clock clock)
            {
                _currentState = clock.CurrentState;
            }
        }

        private void OnCompleted(object sender, EventArgs e)
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private BitmapCache GetBitmapCache()
        {
            return new BitmapCache(VisualTreeHelper.GetDpi(_element).PixelsPerDip);
        }

        private static readonly BitmapCache _defaultBitmapCache;

        private readonly FrameworkElement _element;
        private readonly Storyboard _storyboard;

        private ClockState _currentState = ClockState.Stopped;
    }
}

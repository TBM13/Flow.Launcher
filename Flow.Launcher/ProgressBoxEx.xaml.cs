using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.PluginSDK.Logging;

namespace Flow.Launcher
{
    public partial class ProgressBoxEx : Window
    {
        // TODO: Check if there is any better alternative
        private static readonly Logger<ProgressBoxEx> _logger = Ioc.Default.GetRequiredService<Logger<ProgressBoxEx>>();
        private readonly Action? _cancelProgress;

        private ProgressBoxEx(Action? cancelProgress)
        {
            _cancelProgress = cancelProgress;
            InitializeComponent();
        }

        public static async Task ShowAsync(string caption, Func<Action<double>?, Task> reportProgressAsync, Action? cancelProgress = null)
        {
            ProgressBoxEx? progressBox = null;
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        progressBox = new ProgressBoxEx(cancelProgress)
                        {
                            Title = caption
                        };
                        progressBox.TitleTextBlock.Text = caption;
                        progressBox.Show();
                    });
                }
                else
                {
                    progressBox = new ProgressBoxEx(cancelProgress)
                    {
                        Title = caption
                    };
                    progressBox.TitleTextBlock.Text = caption;
                    progressBox.Show();
                }

                await reportProgressAsync(progressBox.ReportProgress).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError($"An error occurred: {e.Message}");

                await reportProgressAsync(null).ConfigureAwait(false);
            }
            finally
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        progressBox?.Close();
                    });
                }
                else
                {
                    progressBox?.Close();
                }
            }
        }

        private void ReportProgress(double progress)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ReportProgress(progress));
                return;
            }

            if (progress < 0)
            {
                ProgressBar.Value = 0;
            }
            else if (progress >= 100)
            {
                ProgressBar.Value = 100;
                Close();
            }
            else
            {
                ProgressBar.Value = progress;
            }
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            ForceClose();
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            ForceClose();
        }

        private void Button_Minimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Background(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ForceClose()
        {
            Close();
            _cancelProgress?.Invoke();
        }
    }
}

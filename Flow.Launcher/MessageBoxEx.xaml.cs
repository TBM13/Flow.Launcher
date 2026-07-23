using System.IO;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Image;

namespace Flow.Launcher
{
    public partial class MessageBoxEx : Window
    {
        private readonly ImageLoader _imageLoader;

        private MessageBoxResult _result = MessageBoxResult.None;
        private readonly MessageBoxButton _button;

        private MessageBoxEx(ImageLoader imageLoader, MessageBoxButton button)
        {
            _imageLoader = imageLoader;
            _button = button;

            InitializeComponent();
        }

        public static MessageBoxResult Show(
            ImageLoader imageLoader,
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.OK)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(
                    () => Show(imageLoader, messageBoxText, caption, button, icon, defaultResult));
            }

            MessageBoxEx msgbox = new(imageLoader, button);
            if (caption == string.Empty && icon == MessageBoxImage.None)
            {
                // If no caption and no icon, use DescOnlyTextBlock for vertically centered text
                msgbox.Title = messageBoxText;
                msgbox.DescOnlyTextBlock.Visibility = Visibility.Visible;
                msgbox.DescOnlyTextBlock.Text = messageBoxText;
            }
            else
            {
                msgbox.Title = caption;
                msgbox.TitleTextBlock.Text = caption;
                msgbox.DescTextBlock.Text = messageBoxText;
                _ = msgbox.SetImageOfMessageBoxAsync(icon);
            }

            msgbox.SetButtonVisibilityFocusAndResult(button, defaultResult);
            msgbox.ShowDialog();
            return msgbox._result;
        }

        private void SetButtonVisibilityFocusAndResult(MessageBoxButton button, MessageBoxResult defaultResult)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    btnCancel.Visibility = Visibility.Collapsed;
                    btnNo.Visibility = Visibility.Collapsed;
                    btnYes.Visibility = Visibility.Collapsed;
                    btnOk.Focus();
                    _result = MessageBoxResult.OK;
                    break;
                case MessageBoxButton.OKCancel:
                    btnNo.Visibility = Visibility.Collapsed;
                    btnYes.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.Cancel)
                    {
                        btnCancel.Focus();
                        _result = MessageBoxResult.Cancel;
                    }
                    else
                    {
                        btnOk.Focus();
                        _result = MessageBoxResult.OK;
                    }
                    break;
                case MessageBoxButton.YesNo:
                    btnOk.Visibility = Visibility.Collapsed;
                    btnCancel.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.No)
                    {
                        btnNo.Focus();
                        _result = MessageBoxResult.No;
                    }
                    else if (defaultResult == MessageBoxResult.Yes)
                    {
                        btnYes.Focus();
                        _result = MessageBoxResult.Yes;
                    }
                    break;
                case MessageBoxButton.YesNoCancel:
                    btnOk.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.No)
                    {
                        btnNo.Focus();
                        _result = MessageBoxResult.No;
                    }
                    else if (defaultResult == MessageBoxResult.Cancel)
                    {
                        btnCancel.Focus();
                        _result = MessageBoxResult.Cancel;
                    }
                    else
                    {
                        btnYes.Focus();
                        _result = MessageBoxResult.Yes;
                    }
                    break;
                default:
                    break;
            }
        }

        private async Task SetImageOfMessageBoxAsync(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Exclamation:
                    await SetImageAsync("Exclamation.png");
                    Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Question:
                    await SetImageAsync("Question.png");
                    Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Information:
                    await SetImageAsync("Information.png");
                    Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Error:
                    await SetImageAsync("Error.png");
                    Img.Visibility = Visibility.Visible;
                    break;
                default:
                    Img.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async Task SetImageAsync(string imageName)
        {
            var imagePath = Path.Combine(Constant.ProgramDirectory, "Images", imageName);
            var imageSource = await _imageLoader.LoadAsync(imagePath);
            Img.Source = imageSource;
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            if (_button == MessageBoxButton.YesNo)
                // Follow System.Windows.MessageBox behavior
                return;
            else if (_button == MessageBoxButton.OK)
                _result = MessageBoxResult.OK;
            else
                _result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnOk)
                _result = MessageBoxResult.OK;
            else if (sender == btnYes)
                _result = MessageBoxResult.Yes;
            else if (sender == btnNo)
                _result = MessageBoxResult.No;
            else if (sender == btnCancel)
                _result = MessageBoxResult.Cancel;
            else
                _result = MessageBoxResult.None;
            Close();
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (_button == MessageBoxButton.YesNo)
                // Follow System.Windows.MessageBox behavior
                return;
            else if (_button == MessageBoxButton.OK)
                _result = MessageBoxResult.OK;
            else
                _result = MessageBoxResult.Cancel;
            Close();
        }

        private void MessageBoxWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_result != MessageBoxResult.None)
                return;

            if (_button == MessageBoxButton.YesNo)
                // Follow System.Windows.MessageBox behavior
                e.Cancel = true;
            else if (_button == MessageBoxButton.OK)
                _result = MessageBoxResult.OK;
            else
                _result = MessageBoxResult.Cancel;
        }
    }
}

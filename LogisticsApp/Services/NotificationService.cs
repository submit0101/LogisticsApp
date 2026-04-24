using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LogisticsApp.Services;

public class NotificationService
{
    public void Success(string message) => ShowToast("УСПЕХ", message, "#4CAF50");
    public void Warning(string message) => ShowToast("ВНИМАНИЕ", message, "#FF9800");
    public void Error(string message) => ShowToast("ОШИБКА", message, "#F44336");
    public void Info(string message) => ShowToast("ИНФОРМАЦИЯ", message, "#2196F3");

    private void ShowToast(string title, string message, string hexColor)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;

            if (activeWindow == null) return;

            var color = (Brush)new BrushConverter().ConvertFrom(hexColor)!;

            var toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 420,
                SizeToContent = SizeToContent.Height,
                Opacity = 0,
                Owner = activeWindow
            };

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = color,
                BorderThickness = new Thickness(5, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(15),
                Padding = new Thickness(15, 12, 15, 15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 15, ShadowDepth = 4, Opacity = 0.25 }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                Margin = new Thickness(0, 0, 0, 5),
                FontSize = 13
            };

            var msgBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#424242")!
            };

            grid.Children.Add(titleBlock);
            grid.Children.Add(msgBlock);
            Grid.SetRow(titleBlock, 0);
            Grid.SetRow(msgBlock, 1);

            border.Child = grid;
            toast.Content = border;

            toast.Loaded += (s, e) =>
            {
                toast.Left = activeWindow.Left + (activeWindow.ActualWidth - toast.Width) / 2;
                toast.Top = activeWindow.Top + activeWindow.ActualHeight - toast.ActualHeight - 20;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                Task.Delay(4000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                        fadeOut.Completed += (sender, args) => toast.Close();
                        toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    });
                });
            };

            toast.Show();
        });
    }
}
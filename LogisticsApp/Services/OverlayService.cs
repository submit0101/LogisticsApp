using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LogisticsApp.Services;

public class OverlayService
{
    public async Task ExecuteWithOverlayAsync(Func<Task> action, string message = "Пожалуйста, подождите...")
    {
        ShowOverlay(message);
        try
        {
            await action();
        }
        finally
        {
            HideOverlay();
        }
    }

    public void UpdateMessage(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var textBlock = FindElement<TextBlock>("OverlayText");
            if (textBlock != null)
            {
                textBlock.Text = message;
            }
        });
    }

    private void ShowOverlay(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var overlayGrid = FindElement<Grid>("OverlayGrid");
            var textBlock = FindElement<TextBlock>("OverlayText");

            if (overlayGrid != null) overlayGrid.Visibility = Visibility.Visible;
            if (textBlock != null) textBlock.Text = message;
        });
    }

    private void HideOverlay()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var overlayGrid = FindElement<Grid>("OverlayGrid");
            if (overlayGrid != null) overlayGrid.Visibility = Visibility.Collapsed;
        });
    }

    private T? FindElement<T>(string name) where T : FrameworkElement
    {
        var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;

        if (activeWindow?.FindName(name) is T activeElement)
        {
            return activeElement;
        }

        return Application.Current.MainWindow?.FindName(name) as T;
    }
}
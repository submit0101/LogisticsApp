using System.ComponentModel;
using System.Windows;
using LogisticsApp.Models;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using LogisticsApp.ViewModels;
using LogisticsApp.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Views;

public partial class MainWindow
{
    public MainWindow(User user)
    {
        InitializeComponent();
        DataContext = new MainViewModel(user, App.AppHost!.Services);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settingsService = App.AppHost!.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Current;
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;
        }
        if (settings.IsMaximized)
        {
            this.WindowState = WindowState.Maximized;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        var settingsService = App.AppHost!.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Current;
        settings.IsMaximized = this.WindowState == WindowState.Maximized;
        if (this.WindowState == WindowState.Normal)
        {
            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;
        }
        settingsService.Save();
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        var dialogService = App.AppHost!.Services.GetRequiredService<IDialogService>();

        if (dialogService.ShowConfirmation("Выход из системы", "Вы действительно хотите выйти из учетной записи?"))
        {
            var authService = App.AppHost!.Services.GetRequiredService<IAuthService>();
            authService.ClearCredentials();

            var loginWindow = App.AppHost!.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            this.Close();
        }
    }
}
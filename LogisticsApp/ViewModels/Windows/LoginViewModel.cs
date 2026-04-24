using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using LogisticsApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.ViewModels.Windows;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly SecurityService _securityService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(IAuthService authService, SecurityService securityService, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _securityService = securityService;
        _serviceProvider = serviceProvider;
    }

    public void LoadSavedCredentials(PasswordBox passwordBox)
    {
        if (_authService.LoadCredentials(out string savedUser, out string savedPass))
        {
            Username = savedUser;
            passwordBox.Password = savedPass;
            RememberMe = true;
        }
    }

    [RelayCommand]
    private async Task LoginAsync(object parameter)
    {
        if (parameter is not PasswordBox passwordBox) return;

        var password = passwordBox.Password;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Пожалуйста, введите логин и пароль.";
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            await Task.Delay(500);

            var user = await _authService.AuthenticateAsync(Username, password);

            if (user != null)
            {
                _securityService.Initialize(user);

                if (RememberMe)
                {
                    _authService.SaveCredentials(Username, password);
                }
                // Открываем главное окно
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

                // Передаем текущего пользователя во ViewModel главного окна (через конструктор или инициализатор)
                var mainVm = new MainViewModel(user, _serviceProvider);
                mainWindow.DataContext = mainVm;

                mainWindow.Show();

                // Закрываем окно логина (находим его через визуальное дерево или Application.Current)
                Application.Current.MainWindow = mainWindow;
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is LogisticsApp.Views.Windows.LoginWindow)
                    {
                        window.Close();
                        break;
                    }
                }
            }
            else
            {
                ErrorMessage = "Неверный логин или пароль, либо учетная запись заблокирована.";
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Произошла ошибка при подключении к базе данных.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Exit(Window window)
    {
        Application.Current.Shutdown();
    }
}
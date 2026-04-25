using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels.Windows;

public sealed partial class UserEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IAuthService _authService;
    private User _currentUser = new();
    private bool _isEditMode;

    public event Action<bool>? RequestClose;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Логин обязателен")]
    [MinLength(3, ErrorMessage = "Логин от 3 символов")]
    private string _login = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "ФИО обязательно")]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _plainPassword = string.Empty;

    // Свойства AvailableRoles и SelectedRole удалены

    public UserEditorViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, IAuthService authService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _authService = authService;
    }

    public void Initialize(User? user)
    {
        _isEditMode = user is not null;
        _currentUser = user ?? new User();

        Login = _currentUser.Login;
        FullName = _currentUser.FullName ?? string.Empty;

        // Вызов LoadRolesAsync удален
        ValidateAllProperties();
    }

    // Метод LoadRolesAsync полностью удален

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();

        if (HasErrors) // Проверка SelectedRole удалена
        {
            _notify.Warning("Исправьте ошибки заполнения формы.");
            return;
        }

        if (!_isEditMode && string.IsNullOrWhiteSpace(PlainPassword))
        {
            _notify.Warning("Для нового пользователя пароль обязателен.");
            return;
        }

        _currentUser.Login = Login;
        _currentUser.FullName = FullName;
        // Присвоение RoleID и Role удалено

        if (!string.IsNullOrWhiteSpace(PlainPassword))
        {
            _currentUser.PasswordHash = _authService.HashPassword(PlainPassword);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    public User GetUser() => _currentUser;
}
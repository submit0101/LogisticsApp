using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

    [ObservableProperty]
    private ObservableCollection<Role> _availableRoles = [];

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Роль обязательна")]
    private Role? _selectedRole;

    public UserEditorViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, IAuthService authService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _authService = authService;
    }

    public void Initialize(User? user)
    {
        _isLoading = true;
        _isEditMode = user is not null;
        _currentUser = user ?? new User();

        Login = _currentUser.Login;
        FullName = _currentUser.FullName ?? string.Empty;

        _ = LoadRolesAsync(_currentUser.RoleID);
    }

    private async Task LoadRolesAsync(int preselectedRoleId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var roles = await context.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync().ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableRoles.Clear();
                foreach (var r in roles) AvailableRoles.Add(r);

                if (preselectedRoleId > 0)
                {
                    SelectedRole = AvailableRoles.FirstOrDefault(r => r.RoleID == preselectedRoleId);
                }
                else if (AvailableRoles.Any())
                {
                    SelectedRole = AvailableRoles.First();
                }

                ValidateAllProperties();
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();

        if (HasErrors || SelectedRole is null)
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
        _currentUser.RoleID = SelectedRole.RoleID;
        _currentUser.Role = null;

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
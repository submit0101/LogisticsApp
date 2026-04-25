using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public sealed partial class UsersViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IDialogService _dialogService;
    private readonly SecurityService _security;

    [ObservableProperty] private ObservableCollection<User> _users = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteUserCommand))]
    private User? _selectedUser;

    public UsersViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        IDialogService dialogService,
        SecurityService security)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _dialogService = dialogService;
        _security = security;

        _ = LoadUsersAsync();
    }

    public async Task InitializeAsync() => await LoadUsersAsync();

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        // Убрали .Include(u => u.Role), так как ролей больше нет
        var users = await context.Users.AsNoTracking().ToListAsync().ConfigureAwait(false);

        Application.Current.Dispatcher.Invoke(() =>
        {
            Users.Clear();
            foreach (var u in users) Users.Add(u);
        });
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        var newUser = new User();

        if (_dialogService.ShowUserEditor(out string? newPasswordHash, newUser) && !string.IsNullOrEmpty(newPasswordHash))
        {
            await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

            newUser.PasswordHash = newPasswordHash;
            context.Users.Add(newUser);
            await context.SaveChangesAsync().ConfigureAwait(false);

            Serilog.Log.Information("Создан новый пользователь: {User}", newUser.Login);
            await LoadUsersAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedUser))]
    private async Task EditUserAsync()
    {
        if (SelectedUser is null) return;

        if (_dialogService.ShowUserEditor(out string? newPasswordHash, SelectedUser))
        {
            await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var user = await context.Users.FindAsync(SelectedUser.UserID).ConfigureAwait(false);

            if (user is not null)
            {
                user.FullName = SelectedUser.FullName;
                // Убрали обновление RoleID

                if (!string.IsNullOrEmpty(newPasswordHash)) user.PasswordHash = newPasswordHash;

                await context.SaveChangesAsync().ConfigureAwait(false);
                Serilog.Log.Information("Обновлены данные пользователя: {User}", user.Login);
                await LoadUsersAsync();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedUser))]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser is null) return;

        if (_security.CurrentUser?.UserID == SelectedUser.UserID)
        {
            _notify.Error("Критическая блокировка: Вы не можете удалить собственную учетную запись.");
            return;
        }

        // Проверка на последнего администратора удалена, так как ролей больше нет

        if (_dialogService.ShowConfirmation("Удаление", $"Удалить доступ для сотрудника {SelectedUser.Login}?"))
        {
            await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var user = await context.Users.FindAsync(SelectedUser.UserID).ConfigureAwait(false);

            if (user is not null)
            {
                context.Users.Remove(user);
                await context.SaveChangesAsync().ConfigureAwait(false);

                Serilog.Log.Warning("Пользователь {DeletedUser} был удален администратором {Admin}", SelectedUser.Login, _security.CurrentUser?.Login);
                await LoadUsersAsync();
            }
        }
    }

    private bool HasSelectedUser() => SelectedUser is not null;
}
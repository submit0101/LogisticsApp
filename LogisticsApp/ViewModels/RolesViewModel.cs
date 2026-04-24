using System;
using System.Collections.Generic;
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

public sealed class PermissionToggle : ObservableObject
{
    public Permission Permission { get; }
    private bool _isGranted;

    public bool IsGranted
    {
        get => _isGranted;
        set => SetProperty(ref _isGranted, value);
    }

    public PermissionToggle(Permission permission, bool isGranted)
    {
        Permission = permission;
        _isGranted = isGranted;
    }
}

public sealed class ModuleGroup : ObservableObject
{
    public string ModuleName { get; }
    public ObservableCollection<PermissionToggle> Permissions { get; }

    public ModuleGroup(string moduleName, IEnumerable<PermissionToggle> permissions)
    {
        ModuleName = moduleName;
        Permissions = new ObservableCollection<PermissionToggle>(permissions);
    }
}

public sealed partial class RolesViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly OverlayService _overlay;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<Role> _roles = [];
    [ObservableProperty] private ObservableCollection<ModuleGroup> _moduleGroups = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRoleCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRoleCommand))]
    private Role? _selectedRole;

    [ObservableProperty] private string _editingRoleName = string.Empty;
    [ObservableProperty] private string _editingRoleDescription = string.Empty;
    [ObservableProperty] private bool _isEditingSystemRole;

    public RolesViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, OverlayService overlay)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _overlay = overlay;

        // Инициализация загрузки данных при создании ViewModel
        _ = LoadRolesAsync();
    }

    public async Task InitializeAsync() => await LoadRolesAsync();

    [RelayCommand]
    private async Task LoadRolesAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var dbRoles = await context.Roles.AsNoTracking().OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToListAsync();

            Roles.Clear();
            foreach (var r in dbRoles) Roles.Add(r);

            if (SelectedRole == null && Roles.Any())
            {
                SelectedRole = Roles.First();
            }
            else if (SelectedRole != null)
            {
                SelectedRole = Roles.FirstOrDefault(r => r.RoleID == SelectedRole.RoleID) ?? Roles.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedRoleChanged(Role? value)
    {
        if (value is not null)
        {
            EditingRoleName = value.Name;
            EditingRoleDescription = value.Description ?? string.Empty;
            IsEditingSystemRole = value.IsSystem;

            _ = BuildPermissionMatrixAsync(value.RoleID);
        }
        else
        {
            EditingRoleName = string.Empty;
            EditingRoleDescription = string.Empty;
            IsEditingSystemRole = false;
            ModuleGroups.Clear();
        }
    }

    private async Task BuildPermissionMatrixAsync(int roleId)
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var allPermissions = await context.Permissions.AsNoTracking().OrderBy(p => p.PermissionID).ToListAsync();
            var grantedPermissionIds = await context.RolePermissions.AsNoTracking().Where(rp => rp.RoleID == roleId).Select(rp => rp.PermissionID).ToListAsync();

            var grouped = allPermissions
                .GroupBy(p => p.Module)
                .Select(g => new ModuleGroup(
                    g.Key,
                    g.Select(p => new PermissionToggle(p, grantedPermissionIds.Contains(p.PermissionID)))
                ))
                .OrderBy(g => g.ModuleName)
                .ToList();

            ModuleGroups.Clear();
            foreach (var mg in grouped) ModuleGroups.Add(mg);
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddRoleAsync()
    {
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var newRole = new Role { Name = "Новая Роль", Description = "Описание роли", IsSystem = false };

            context.Roles.Add(newRole);
            await context.SaveChangesAsync();

            SelectedRole = null;
            await LoadRolesAsync();
            SelectedRole = Roles.FirstOrDefault(r => r.RoleID == newRole.RoleID);
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteRole))]
    private async Task DeleteRoleAsync()
    {
        if (SelectedRole is null) return;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            if (await context.Users.AnyAsync(u => u.RoleID == SelectedRole.RoleID))
            {
                _notify.Warning("Нельзя удалить роль, так как к ней привязаны пользователи. Сначала измените роли у пользователей.");
                return;
            }

            var role = await context.Roles.FindAsync(SelectedRole.RoleID);
            if (role is not null)
            {
                context.Roles.Remove(role);
                await context.SaveChangesAsync();

                _notify.Success("Роль успешно удалена из системы.");
                SelectedRole = null;
                await LoadRolesAsync();
            }
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
    }

    private bool CanDeleteRole() => SelectedRole is not null && !SelectedRole.IsSystem;

    [RelayCommand(CanExecute = nameof(CanSaveRole))]
    private async Task SaveRoleAsync()
    {
        if (SelectedRole is null || string.IsNullOrWhiteSpace(EditingRoleName)) return;

        int currentRoleId = SelectedRole.RoleID;
        string newName = EditingRoleName;
        string newDesc = EditingRoleDescription;

        var selectedPermissions = ModuleGroups
            .SelectMany(mg => mg.Permissions)
            .Where(pt => pt.IsGranted)
            .Select(pt => pt.Permission.PermissionID)
            .ToList();

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();
                var role = await context.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.RoleID == currentRoleId);

                if (role is null) return;

                role.Name = newName;
                role.Description = newDesc;

                if (!role.IsSystem)
                {
                    context.RolePermissions.RemoveRange(role.RolePermissions);
                    var newRolePermissions = selectedPermissions
                        .Select(pid => new RolePermission { RoleID = role.RoleID, PermissionID = pid })
                        .ToList();

                    context.RolePermissions.AddRange(newRolePermissions);
                }

                await context.SaveChangesAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _notify.Success("Настройки роли и права доступа сохранены");
                    _ = LoadRolesAsync();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
            }
        }, "Сохранение матрицы прав...");
    }

    private bool CanSaveRole() => SelectedRole is not null;
}
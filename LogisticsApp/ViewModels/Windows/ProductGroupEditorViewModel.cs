using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels.Windows;

public partial class ProductGroupEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;

    private ProductGroup _currentGroup = new();

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Наименование группы обязательно")]
    private string _name = string.Empty;

    [ObservableProperty] private string _description = string.Empty;

    public ProductGroupEditorViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        SecurityService security)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _security = security;
    }

    public void Initialize(ProductGroup? existingGroup)
    {
        if (existingGroup != null)
        {
            _currentGroup = existingGroup;
            Name = existingGroup.Name;
            Description = existingGroup.Description ?? string.Empty;
        }
        else
        {
            _currentGroup = new ProductGroup();
        }
    }

    public ProductGroup GetGroup() => _currentGroup;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        IsLoading = true;

        _currentGroup.Name = Name;
        _currentGroup.Description = Description;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (_currentGroup.GroupID == 0)
            {
                context.ProductGroups.Add(_currentGroup);
            }
            else
            {
                context.ProductGroups.Update(_currentGroup);
            }

            var auditLog = new AuditLog
            {
                Action = _currentGroup.GroupID == 0 ? "Создание" : "Изменение",
                EntityName = "Товарные группы",
                Details = $"Сотрудник {_security.CurrentUser?.Login ?? "Система"} сохранил товарную группу: {Name}",
                Timestamp = DateTime.Now,
                UserID = _security.CurrentUser?.UserID
            };
            context.AuditLogs.Add(auditLog);

            await context.SaveChangesAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success("Товарная группа успешно сохранена");
                RequestClose?.Invoke(true);
            });
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error($"Ошибка БД: {ex.Message}"));
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
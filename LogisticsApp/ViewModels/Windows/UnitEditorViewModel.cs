using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace LogisticsApp.ViewModels.Windows;

public partial class UnitEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;

    private Unit _currentUnit = new();

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Краткое наименование обязательно")]
    private string _name = string.Empty;

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _code = string.Empty;

    public UnitEditorViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        SecurityService security)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _security = security;
    }

    public void Initialize(Unit? existingUnit)
    {
        if (existingUnit != null)
        {
            _currentUnit = existingUnit;
            Name = existingUnit.Name;
            FullName = existingUnit.FullName;
            Code = existingUnit.Code;
        }
        else
        {
            _currentUnit = new Unit();
        }
    }

    public Unit GetUnit() => _currentUnit;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        IsLoading = true;

        _currentUnit.Name = Name;
        _currentUnit.FullName = FullName;
        _currentUnit.Code = Code;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (_currentUnit.UnitID == 0)
            {
                context.Units.Add(_currentUnit);
            }
            else
            {
                context.Units.Update(_currentUnit);
            }

            var auditLog = new AuditLog
            {
                Action = _currentUnit.UnitID == 0 ? "Создание" : "Изменение",
                EntityName = "Единицы измерения",
                Details = $"Сотрудник {_security.CurrentUser?.Login ?? "Система"} сохранил ЕИ: {Name}",
                Timestamp = DateTime.Now,
                UserID = _security.CurrentUser?.UserID
            };
            context.AuditLogs.Add(auditLog);

            await context.SaveChangesAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success("Единица измерения успешно сохранена");
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
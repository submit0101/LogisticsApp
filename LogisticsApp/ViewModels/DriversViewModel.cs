using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.DTOs;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public partial class DriversViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly IDialogService _dialogService;
    private readonly SecurityService _security;
    private readonly NotificationService _notify;
    private readonly ISettingsService _settingsService;
    private readonly ExcelExportService _exportService;
    private readonly object _driversLock = new();

    public ObservableCollection<Driver> Drivers { get; } = new();

    public int ExpiryWarningDays => _settingsService.Current.DocumentExpiryWarningDays;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _highlightText = string.Empty;

    [ObservableProperty]
    private DriverStatus? _selectedStatusFilter;

    [ObservableProperty]
    private string? _selectedCategoryFilter;

    [ObservableProperty]
    private DateTime? _employmentDateFrom;

    [ObservableProperty]
    private DateTime? _employmentDateTo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditDriverCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteDriverCommand))]
    private Driver? _selectedDriver;

    public Array AvailableStatuses => Enum.GetValues(typeof(DriverStatus));
    public List<string> AvailableCategories { get; } = new() { "Все категории", "B", "C", "D", "E", "CE", "C, CE" };

    public DriversViewModel(
        IDbContextFactory<LogisticsDbContext> dbContextFactory,
        IDialogService dialogService,
        SecurityService security,
        NotificationService notify,
        ISettingsService settingsService,
        ExcelExportService exportService)
    {
        _dbContextFactory = dbContextFactory;
        _dialogService = dialogService;
        _security = security;
        _notify = notify;
        _settingsService = settingsService;
        _exportService = exportService;

        _selectedCategoryFilter = "Все категории";

        BindingOperations.EnableCollectionSynchronization(Drivers, _driversLock);
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewDrivers)) return;

        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.Drivers.AsNoTracking().AsQueryable();

            //if (!string.IsNullOrWhiteSpace(HighlightText))
            //{
            //    var search = HighlightText.ToLower();
            //    query = query.Where(d =>
            //        d.LastName.ToLower().Contains(search) ||
            //        d.FirstName.ToLower().Contains(search) ||
            //        d.LicenseNumber.ToLower().Contains(search) ||
            //        d.Phone.Contains(search));
            //}

            if (SelectedStatusFilter.HasValue)
            {
                query = query.Where(d => d.Status == SelectedStatusFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && SelectedCategoryFilter != "Все категории")
            {
                query = query.Where(d => d.LicenseCategories.Contains(SelectedCategoryFilter));
            }

            if (EmploymentDateFrom.HasValue)
            {
                query = query.Where(d => d.EmploymentDate >= EmploymentDateFrom.Value);
            }

            if (EmploymentDateTo.HasValue)
            {
                var dateTo = EmploymentDateTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(d => d.EmploymentDate <= dateTo);
            }

            var results = await query.OrderBy(d => d.LastName).ToListAsync();

            lock (_driversLock)
            {
                Drivers.Clear();
                foreach (var r in results)
                {
                    Drivers.Add(r);
                }
            }

            OnPropertyChanged(nameof(ExpiryWarningDays));
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка при загрузке данных: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    //partial void OnHighlightTextChanged(string value) => _ = LoadDataAsync();
    partial void OnSelectedStatusFilterChanged(DriverStatus? value) => _ = LoadDataAsync();
    partial void OnSelectedCategoryFilterChanged(string? value) => _ = LoadDataAsync();
    partial void OnEmploymentDateFromChanged(DateTime? value) => _ = LoadDataAsync();
    partial void OnEmploymentDateToChanged(DateTime? value) => _ = LoadDataAsync();

    [RelayCommand]
    private void AddDriver()
    {
        if (_dialogService.ShowDriverEditor(null))
        {
            _ = LoadDataAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void EditDriver()
    {
        if (SelectedDriver != null && _dialogService.ShowDriverEditor(SelectedDriver))
        {
            _ = LoadDataAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteDriverAsync()
    {
        if (SelectedDriver == null) return;
        var confirm = _dialogService.ShowConfirmation("Удаление", $"Пометить водителя {SelectedDriver.FullName} на удаление?");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            bool hasWaybills = await context.Waybills.AnyAsync(w => w.DriverID == SelectedDriver.DriverID);

            if (hasWaybills)
            {
                _dialogService.ShowError("Ошибка", "Невозможно удалить водителя, так как за ним числятся путевые листы.\nРекомендуется просто перевести его в статус 'Уволен'.");
                return;
            }

            var driverToDelete = await context.Drivers.FindAsync(SelectedDriver.DriverID);
            if (driverToDelete != null)
            {
                context.Drivers.Remove(driverToDelete);
                await context.SaveChangesAsync();
                _notify.Success("Водитель помечен на удаление");
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка при удалении: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanEditOrDelete() => SelectedDriver != null;

    [RelayCommand]
    private void ExportExcel()
    {
        if (!_security.HasPermission(AppPermission.ViewDrivers)) return;

        if (Drivers == null || Drivers.Count == 0)
        {
            _notify.Warning("Нет данных для экспорта.");
            return;
        }

        var exportData = Drivers.Select(d => new DriverExportDto
        {
            FullName = d.FullName,
            Phone = d.Phone,
            Email = d.Email ?? string.Empty,
            LicenseNumber = d.LicenseNumber,
            LicenseCategories = d.LicenseCategories,
            LicenseExpirationDate = d.LicenseExpirationDate.ToString("dd.MM.yyyy"),
            MedicalCertificateNumber = d.MedicalCertificateNumber ?? string.Empty,
            MedicalCertificateExpiration = d.MedicalCertificateExpiration.HasValue ? d.MedicalCertificateExpiration.Value.ToString("dd.MM.yyyy") : string.Empty,
            EmploymentDate = d.EmploymentDate.ToString("dd.MM.yyyy"),
            Status = d.Status switch
            {
                DriverStatus.Active => "Активен",
                DriverStatus.OnLeave => "В отпуске",
                DriverStatus.SickLeave => "На больничном",
                DriverStatus.Dismissed => "Уволен",
                _ => d.Status.ToString()
            }
        }).ToList();

        _exportService.Export(exportData, "Справочник_Водители");
    }
}
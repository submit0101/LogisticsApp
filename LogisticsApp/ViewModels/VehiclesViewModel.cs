using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
using Microsoft.Win32;

namespace LogisticsApp.ViewModels;

public partial class VehiclesViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly IDialogService _dialogService;
    private readonly SecurityService _security;
    private readonly NotificationService _notify;
    private readonly OverlayService _overlay;
    private readonly ExcelExportService _exportService;
    private readonly ExcelImportService _importService;
    private readonly object _vehiclesLock = new();

    public ObservableCollection<Vehicle> Vehicles { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _highlightText = string.Empty;

    [ObservableProperty]
    private VehicleStatus? _selectedStatusFilter;

    [ObservableProperty]
    private bool? _isFridgeFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditVehicleCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteVehicleCommand))]
    private Vehicle? _selectedVehicle;

    public Array AvailableStatuses => Enum.GetValues(typeof(VehicleStatus));

    public List<string> AvailableFridgeFilters { get; } = new()
    {
        "Все типы",
        "Только рефрижераторы",
        "Обычные фургоны/тенты"
    };

    private string _selectedFridgeFilterStr = "Все типы";

    public string SelectedFridgeFilterStr
    {
        get => _selectedFridgeFilterStr;
        set
        {
            if (SetProperty(ref _selectedFridgeFilterStr, value))
            {
                IsFridgeFilter = value == "Только рефрижераторы" ? true : (value == "Обычные фургоны/тенты" ? false : null);
                _ = LoadDataAsync();
            }
        }
    }

    public VehiclesViewModel(
        IDbContextFactory<LogisticsDbContext> dbContextFactory,
        IDialogService dialogService,
        SecurityService security,
        NotificationService notify,
        OverlayService overlay,
        ExcelExportService exportService,
        ExcelImportService importService)
    {
        _dbContextFactory = dbContextFactory;
        _dialogService = dialogService;
        _security = security;
        _notify = notify;
        _overlay = overlay;
        _exportService = exportService;
        _importService = importService;

        BindingOperations.EnableCollectionSynchronization(Vehicles, _vehiclesLock);
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewVehicles)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.Vehicles
                .Include(v => v.ServiceRecords)
                .AsNoTracking()
                .AsSplitQuery()
                .AsQueryable();

            //if (!string.IsNullOrWhiteSpace(HighlightText))
            //{
            //    var search = HighlightText.ToLower();
            //    query = query.Where(v =>
            //        v.RegNumber.ToLower().Contains(search) ||
            //        v.Model.ToLower().Contains(search) ||
            //        v.VIN.ToLower().Contains(search));
            //}

            if (SelectedStatusFilter.HasValue)
            {
                query = query.Where(v => v.Status == SelectedStatusFilter.Value);
            }

            if (IsFridgeFilter.HasValue)
            {
                query = query.Where(v => v.IsFridge == IsFridgeFilter.Value);
            }

            var results = await query.OrderBy(v => v.Status).ThenBy(v => v.RegNumber).ToListAsync();

            lock (_vehiclesLock)
            {
                Vehicles.Clear();
                foreach (var r in results)
                {
                    Vehicles.Add(r);
                }
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

    //partial void OnHighlightTextChanged(string value) => _ = LoadDataAsync();
    partial void OnSelectedStatusFilterChanged(VehicleStatus? value) => _ = LoadDataAsync();

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void AddVehicle()
    {
        if (_dialogService.ShowVehicleEditor(null))
        {
            _ = LoadDataAsync();
        }
    }

    private bool CanAdd() => _security.HasPermission(AppPermission.EditVehicles);

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void EditVehicle()
    {
        if (SelectedVehicle != null && _dialogService.ShowVehicleEditor(SelectedVehicle))
        {
            _ = LoadDataAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteVehicleAsync()
    {
        if (SelectedVehicle == null) return;

        if (!_dialogService.ShowConfirmation("Подтверждение", $"Пометить транспортное средство {SelectedVehicle.RegNumber} на удаление?"))
            return;

        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            bool hasWaybills = await context.Waybills.AnyAsync(w => w.VehicleID == SelectedVehicle.VehicleID);

            if (hasWaybills)
            {
                if (_dialogService.ShowConfirmation("Внимание", "Автомобиль имеет историю рейсов. Удаление невозможно. Перевести транспорт в статус 'Списан'?"))
                {
                    var vehicleToArchive = await context.Vehicles.FindAsync(SelectedVehicle.VehicleID);
                    if (vehicleToArchive != null)
                    {
                        vehicleToArchive.Status = VehicleStatus.Decommissioned;
                        await context.SaveChangesAsync();
                        _notify.Success("Транспорт списан");
                        await LoadDataAsync();
                    }
                }
                return;
            }

            var vehicleToDelete = await context.Vehicles.FindAsync(SelectedVehicle.VehicleID);
            if (vehicleToDelete != null)
            {
                context.Vehicles.Remove(vehicleToDelete);
                await context.SaveChangesAsync();
                _notify.Success("Транспортное средство помечено на удаление");
                await LoadDataAsync();
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

    private bool CanEditOrDelete() => SelectedVehicle != null && _security.HasPermission(AppPermission.EditVehicles);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task ImportExcelAsync()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls" };
        if (openFileDialog.ShowDialog() != true) return;

        string filePath = openFileDialog.FileName;

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            var result = await _importService.ImportVehiclesAsync(filePath);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (result.Errors > 0 && result.Added == 0 && result.Updated == 0)
                {
                    _notify.Error($"Сбой импорта.\n{result.ErrorDetails}");
                }
                else
                {
                    string message = $"Импорт завершен.\nДобавлено: {result.Added}\nОбновлено: {result.Updated}";
                    if (result.Errors > 0)
                    {
                        message += $"\nОшибок: {result.Errors}. Подробности в логе.";
                        _notify.Warning(message);
                        Serilog.Log.Warning("Ошибки при импорте Excel: \n" + result.ErrorDetails);
                    }
                    else
                    {
                        _notify.Success(message);
                    }
                    _ = LoadDataAsync();
                }
            });
        }, "Чтение и импорт файла Excel...");
    }

    [RelayCommand]
    private void ExportExcel()
    {
        if (!_security.HasPermission(AppPermission.ViewVehicles)) return;

        if (Vehicles == null || Vehicles.Count == 0)
        {
            _notify.Warning("Нет данных для экспорта.");
            return;
        }

        var exportData = Vehicles.Select(v => new VehicleExportDto
        {
            RegNumber = v.RegNumber,
            Model = v.Model,
            VIN = v.VIN ?? string.Empty,
            Year = v.Year,
            CapacityKG = v.CapacityKG,
            CapacityM3 = v.CapacityM3,
            Mileage = v.Mileage,
            IsFridge = v.IsFridge ? "Да" : "Нет",
            SanitizationDate = v.SanitizationDate.ToString("dd.MM.yyyy"),
            Status = v.Status switch
            {
                VehicleStatus.Active => "На линии",
                VehicleStatus.InService => "В ремонте / ТО",
                VehicleStatus.Inactive => "В резерве",
                VehicleStatus.Decommissioned => "Списан",
                _ => v.Status.ToString()
            },
            FuelType = v.FuelType switch
            {
                FuelType.AI92 => "АИ-92",
                FuelType.AI95 => "АИ-95",
                FuelType.AI98 => "АИ-98",
                FuelType.DT => "ДТ (Дизель)",
                FuelType.GasPropan => "Газ (Пропан)",
                FuelType.GasMetan => "Газ (Метан)",
                _ => v.FuelType.ToString()
            },
            BaseFuelConsumption = v.BaseFuelConsumption
        }).ToList();

        _exportService.Export(exportData, "Справочник_Автопарк");
    }
}
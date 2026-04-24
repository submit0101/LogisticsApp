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
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public partial class WaybillsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;
    private readonly IDialogService _dialogService;
    private readonly IWaybillDocumentService _waybillDocumentService;
    private readonly object _waybillsLock = new();

    public ObservableCollection<WaybillListDto> Waybills { get; } = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewRouteCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintRouteManifestCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintDriverDocumentCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintLabelsCommand))]
    private WaybillListDto? _selectedWaybill;

    [ObservableProperty] private string _highlightText = string.Empty;
    [ObservableProperty] private int _selectedFilterIndex = 0;
    [ObservableProperty] private ObservableCollection<Vehicle> _vehiclesFilterList = new();
    [ObservableProperty] private Vehicle? _selectedVehicleFilter;
    [ObservableProperty] private ObservableCollection<Driver> _driversFilterList = new();
    [ObservableProperty] private Driver? _selectedDriverFilter;
    [ObservableProperty] private DateTime? _dateFromFilter;
    [ObservableProperty] private DateTime? _dateToFilter;

    public WaybillsViewModel(IDbContextFactory<LogisticsDbContext> dbContextFactory, NotificationService notify, SecurityService security, IDialogService dialogService, IWaybillDocumentService waybillDocumentService)
    {
        _dbContextFactory = dbContextFactory;
        _notify = notify;
        _security = security;
        _dialogService = dialogService;
        _waybillDocumentService = waybillDocumentService;
        BindingOperations.EnableCollectionSynchronization(Waybills, _waybillsLock);

        // Автоматически загружаем данные при первом создании (Singleton)
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewWaybills)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            if (VehiclesFilterList.Count == 0)
            {
                var vehicles = await context.Vehicles.AsNoTracking().OrderBy(v => v.RegNumber).ToListAsync();
                var allVehiclesItem = new Vehicle { VehicleID = -1, RegNumber = "Все автомобили", Model = "" };
                Application.Current.Dispatcher.Invoke(() =>
                {
                    VehiclesFilterList.Add(allVehiclesItem);
                    foreach (var v in vehicles) VehiclesFilterList.Add(v);
                    _selectedVehicleFilter = allVehiclesItem;
                });
            }
            if (DriversFilterList.Count == 0)
            {
                var drivers = await context.Drivers.AsNoTracking().OrderBy(d => d.LastName).ToListAsync();
                var allDriversItem = new Driver { DriverID = -1, LastName = "Все водители", FirstName = "" };
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DriversFilterList.Add(allDriversItem);
                    foreach (var d in drivers) DriversFilterList.Add(d);
                    _selectedDriverFilter = allDriversItem;
                });
            }
            var query = context.Waybills.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(HighlightText))
            {
                var search = HighlightText.ToLower();
                query = query.Where(w =>
                    (w.Vehicle != null && w.Vehicle.RegNumber.ToLower().Contains(search)) ||
                    (w.Driver != null && w.Driver.LastName.ToLower().Contains(search)) ||
                    w.WaybillID.ToString() == search);
            }
            switch (SelectedFilterIndex)
            {
                case 1: query = query.Where(w => w.Status == WaybillStatus.Draft); break;
                case 2: query = query.Where(w => w.Status == WaybillStatus.Planned); break;
                case 3: query = query.Where(w => w.Status == WaybillStatus.Active); break;
                case 4: query = query.Where(w => w.Status == WaybillStatus.Completed || w.Status == WaybillStatus.Cancelled); break;
            }
            if (SelectedVehicleFilter != null && SelectedVehicleFilter.VehicleID != -1)
            {
                query = query.Where(w => w.VehicleID == SelectedVehicleFilter.VehicleID);
            }
            if (SelectedDriverFilter != null && SelectedDriverFilter.DriverID != -1)
            {
                query = query.Where(w => w.DriverID == SelectedDriverFilter.DriverID);
            }
            if (DateFromFilter.HasValue)
            {
                query = query.Where(w => w.DateCreate >= DateFromFilter.Value.Date);
            }
            if (DateToFilter.HasValue)
            {
                var toDate = DateToFilter.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(w => w.DateCreate <= toDate);
            }
            var results = await query.Select(w => new WaybillListDto
            {
                WaybillID = w.WaybillID,
                DateCreate = w.DateCreate,
                VehicleRegNumber = w.Vehicle != null ? w.Vehicle.RegNumber : "Не назначен",
                DriverFullName = w.Driver != null ? w.Driver.FullName : "Не назначен",
                DateOut = w.DateOut,
                DateIn = w.DateIn,
                Status = w.Status,
                IsPosted = w.IsPosted
            }).OrderByDescending(w => w.DateCreate).ToListAsync();

            lock (_waybillsLock)
            {
                Waybills.Clear();
                foreach (var r in results) Waybills.Add(r);
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

    partial void OnHighlightTextChanged(string value) => _ = LoadDataAsync();
    partial void OnSelectedFilterIndexChanged(int value) => _ = LoadDataAsync();
    partial void OnSelectedVehicleFilterChanged(Vehicle? value) => _ = LoadDataAsync();
    partial void OnSelectedDriverFilterChanged(Driver? value) => _ = LoadDataAsync();
    partial void OnDateFromFilterChanged(DateTime? value) => _ = LoadDataAsync();
    partial void OnDateToFilterChanged(DateTime? value) => _ = LoadDataAsync();

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (_dialogService.ShowWaybillEditor(null)) _ = LoadDataAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndEditPermission))]
    private async Task EditAsync()
    {
        if (SelectedWaybill == null) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var fullWaybill = await context.Waybills
                .Include(w => w.Vehicle)
                .Include(w => w.Driver)
                .Include(w => w.Points)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(w => w.WaybillID == SelectedWaybill.WaybillID);

            if (fullWaybill != null && _dialogService.ShowWaybillEditor(fullWaybill))
            {
                await LoadDataAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndDeletePermission))]
    private async Task DeleteAsync()
    {
        if (SelectedWaybill == null) return;
        if (_dialogService.ShowConfirmation("Подтверждение", $"Пометить путевой лист №{SelectedWaybill.WaybillID} на удаление?"))
        {
            IsLoading = true;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var strategy = context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        var points = await context.WaybillPoints.Where(wp => wp.WaybillID == SelectedWaybill.WaybillID).ToListAsync();
                        foreach (var point in points)
                        {
                            var order = await context.Orders.FindAsync(point.OrderID);
                            if (order != null) order.Status = "New";
                        }
                        context.WaybillPoints.RemoveRange(points);

                        var waybillToDelete = await context.Waybills.FindAsync(SelectedWaybill.WaybillID);
                        if (waybillToDelete != null)
                        {
                            context.Waybills.Remove(waybillToDelete);
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                _notify.Success("Путевой лист помечен на удаление");
                await LoadDataAsync();
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
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ViewRouteAsync()
    {
        if (SelectedWaybill == null) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var waybill = await context.Waybills
                .Include(w => w.Vehicle)
                .Include(w => w.Driver)
                .Include(w => w.Points)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(w => w.WaybillID == SelectedWaybill.WaybillID);

            if (waybill != null)
            {
                App.Current.Dispatcher.Invoke(() => new Views.RouteMapWindow(waybill).Show());
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task PrintRouteManifestAsync()
    {
        if (SelectedWaybill == null) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var waybill = await context.Waybills
                .Include(w => w.Vehicle)
                .Include(w => w.Driver)
                .Include(w => w.Points)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(w => w.WaybillID == SelectedWaybill.WaybillID);

            if (waybill != null)
            {
                var dto = new RouteManifestDto
                {
                    WaybillNumber = waybill.WaybillID.ToString(),
                    DispatchDate = waybill.DateCreate,
                    DriverFullName = waybill.Driver?.FullName ?? string.Empty,
                    VehicleRegistrationNumber = waybill.Vehicle?.RegNumber ?? string.Empty,
                    VehicleModel = waybill.Vehicle?.Model ?? string.Empty,
                    RouteName = "Доставка клиентам",
                    Points = waybill.Points.OrderBy(p => p.SequenceNumber).Select(p => new RouteManifestPointDto
                    {
                        BoxesCount = 1,
                        CratesCount = 1,
                        OrderNumber = p.OrderID,
                        CustomerName = p.Order?.Customer?.Name ?? string.Empty,
                        CustomerAddress = p.Order?.Customer?.Address ?? string.Empty,
                        NetWeight = (decimal)(p.Order?.WeightKG ?? 0),
                        GrossWeight = (decimal)(p.Order?.WeightKG ?? 0) * 1.05m
                    }).ToList()
                };

                var excelBytes = await _waybillDocumentService.GenerateRouteManifestExcelAsync(dto);
                string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Маршрутный_Лист_{SelectedWaybill.WaybillID}_{Guid.NewGuid():N}.xlsx");
                await System.IO.File.WriteAllBytesAsync(tempFileName, excelBytes);
                _notify.Success("Маршрутный лист открыт для предосмотра");
                new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task PrintDriverDocumentAsync()
    {
        if (SelectedWaybill == null) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var waybill = await context.Waybills
                .Include(w => w.Vehicle)
                .Include(w => w.Driver)
                .Include(w => w.Points)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(w => w.WaybillID == SelectedWaybill.WaybillID);

            if (waybill != null)
            {
                string directorName = "Иванов И.И.";

                var accountants = await context.Users
                    .Include(u => u.Role)
                    .Where(u => u.Role != null && u.Role.Name == "Бухгалтер" && !string.IsNullOrEmpty(u.FullName))
                    .Select(u => u.FullName)
                    .ToListAsync();

                string chiefAccountant = "Петрова П.П.";
                if (accountants.Any())
                {
                    var random = new Random();
                    chiefAccountant = accountants[random.Next(accountants.Count)]!;
                }

                var dto = new DriverDocumentDto
                {
                    DocumentNumber = waybill.WaybillID.ToString(),
                    DocumentDate = waybill.DateCreate,
                    DriverFullName = waybill.Driver?.FullName ?? string.Empty,
                    VehicleRegistrationNumber = waybill.Vehicle?.RegNumber ?? string.Empty,
                    VehicleModel = waybill.Vehicle?.Model ?? string.Empty,
                    RouteName = "Доставка клиентам",
                    ManagerName = directorName,
                    ChiefAccountantName = chiefAccountant,
                    Items = waybill.Points.SelectMany(p => p.Order?.Items ?? Enumerable.Empty<OrderItem>())
                        .GroupBy(i => i.Product)
                        .Select((g, index) => new DriverDocumentItemDto
                        {
                            SequenceNumber = index + 1,
                            ProductCode = g.Key?.SKU ?? string.Empty,
                            ProductName = g.Key?.Name ?? string.Empty,
                            Quantity = (decimal)g.Sum(i => i.Quantity),
                            Weight = (decimal)g.Sum(i => i.TotalWeight)
                        }).ToList()
                };

                var pdfBytes = await _waybillDocumentService.GenerateDriverDocumentPdfAsync(dto);
                string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ТТН_{waybill.WaybillID}_{Guid.NewGuid():N}.pdf");
                await System.IO.File.WriteAllBytesAsync(tempFileName, pdfBytes);
                _notify.Success("ТТН открыта для предосмотра");
                new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task PrintLabelsAsync()
    {
        if (SelectedWaybill == null) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var waybill = await context.Waybills
                .Include(w => w.Vehicle)
                .Include(w => w.Driver)
                .Include(w => w.Points)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(w => w.WaybillID == SelectedWaybill.WaybillID);

            if (waybill != null)
            {
                string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Labels_{SelectedWaybill.WaybillID}_{Guid.NewGuid():N}.pdf");
                new LabelPrintService().GenerateLabelsPdf(waybill, tempFileName);
                _notify.Success("Этикетки открыты для предосмотра");
                new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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

    private bool HasSelection() => SelectedWaybill != null;
    private bool CanAdd() => _security.HasPermission(AppPermission.EditWaybills);
    private bool HasSelectionAndEditPermission() => SelectedWaybill != null && _security.HasPermission(AppPermission.EditWaybills);
    private bool HasSelectionAndDeletePermission() => SelectedWaybill != null && _security.HasPermission(AppPermission.DeleteWaybills);
}
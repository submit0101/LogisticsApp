using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Messages;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LogisticsApp.ViewModels.Windows;

public sealed partial class WaybillEditorViewModel : ViewModelBase, IRecipient<SettingsChangedMessage>, IRecipient<RouteInteractiveUpdateMessage>
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly OverlayService _overlay;
    private readonly GeocodingService _geoService;
    private readonly FuelPriceService _fuelPriceService;
    private readonly WaybillDispatchService _dispatchService;
    private readonly TripValidationService _tripValidationService;
    private readonly RouteCalculationService _routeService;
    private AppSettings _currentSettings;
    private Waybill _currentWaybill = new();
    private bool _isUpdatingTickets;

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<Vehicle> AvailableVehicles { get; } = new();
    public ObservableCollection<Driver> AvailableDrivers { get; } = new();
    public ObservableCollection<Order> AvailableOrders { get; } = new();

    [ObservableProperty] private Order? _selectedOrderToAdd;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Транспортное средство обязательно")]
    private Vehicle? _selectedVehicle;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Водитель обязателен")]
    private Driver? _selectedDriver;

    [ObservableProperty] private WaybillStatus _status = WaybillStatus.Draft;

    public Array AvailableStatuses => Enum.GetValues(typeof(WaybillStatus));
    public Array AvailablePointStatuses => Enum.GetValues(typeof(WaybillPointStatus));

    [ObservableProperty] private DateTime _dateCreate = DateTime.Now;
    [ObservableProperty] private DateTime? _dateOut;
    [ObservableProperty] private DateTime? _timeOut;
    [ObservableProperty] private double? _odometerOut;
    [ObservableProperty] private double? _fuelOut;
    [ObservableProperty] private DateTime? _dateIn;
    [ObservableProperty] private DateTime? _timeIn;
    [ObservableProperty] private double? _odometerIn;
    [ObservableProperty] private double? _fuelIn;
    [ObservableProperty] private double _totalDistance;
    [ObservableProperty] private double _realDriveTimeMinutes;
    [ObservableProperty] private double _calculatedFuelConsumption;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private bool _isPosted;
    [ObservableProperty] private decimal _currentFuelPrice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OptimizeRouteCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePointUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePointDownCommand))]
    private ObservableCollection<WaybillPoint> _points = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MovePointUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePointDownCommand))]
    private WaybillPoint? _selectedPoint;

    [ObservableProperty] private ObservableCollection<FuelTicket> _fuelTickets = new();
    [ObservableProperty] private FuelTicket? _selectedFuelTicket;
    [ObservableProperty] private DateTime _newTicketDate = DateTime.Now;
    [ObservableProperty] private double _newTicketVolume;
    [ObservableProperty] private decimal _newTicketAmount;
    [ObservableProperty] private string _newTicketNumber = string.Empty;

    [ObservableProperty] private double _currentTotalWeight;
    [ObservableProperty] private double _currentTotalVolume;
    [ObservableProperty] private bool _isWeightOverload;
    [ObservableProperty] private bool _isVolumeOverload;

    [ObservableProperty] private DateTime? _departureTime;
    [ObservableProperty] private DateTime? _expectedArrivalTime;
    [ObservableProperty] private DateTime? _actualArrivalTime;

    
    public bool IsDraft => Status == WaybillStatus.Draft;
    public bool IsReady => Status == WaybillStatus.Planned;
    public bool IsInTransit => Status == WaybillStatus.Active;// Больше не проверяет HasIncident
    public bool IsCompleted => Status == WaybillStatus.Completed || Status == WaybillStatus.Cancelled;
    public bool IsEditable => (IsDraft || IsReady) && !IsLoading && !IsPosted;

    public WaybillEditorViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        OverlayService overlay,
        ISettingsService settingsService,
        GeocodingService geoService,
        IMessenger messenger,
        FuelPriceService fuelPriceService,
        WaybillDispatchService dispatchService,
        TripValidationService tripValidationService,
        RouteCalculationService routeService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _overlay = overlay;
        _geoService = geoService;
        _currentSettings = settingsService.Current;
        _fuelPriceService = fuelPriceService;
        _dispatchService = dispatchService;
        _tripValidationService = tripValidationService;
        _routeService = routeService;

        messenger.RegisterAll(this);
    }

    public void Receive(SettingsChangedMessage message)
    {
        _currentSettings = message.Value;
        CalculateLoad();
    }

    public void Receive(RouteInteractiveUpdateMessage message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TotalDistance = Math.Round(message.DistanceKm, 2);
            RealDriveTimeMinutes = message.TimeInTrafficMinutes > 0 ? message.TimeInTrafficMinutes : message.TimeMinutes;
            CalculateMetrics();
        });
    }

    public void Initialize(Waybill? waybill)
    {
        _ = LoadDictionariesAsync(waybill);
    }

    private async Task LoadDictionariesAsync(Waybill? waybill)
    {
        IsLoading = true;
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dbVehicles = await context.Vehicles.Where(v => v.Status == VehicleStatus.Active).OrderBy(v => v.RegNumber).ToListAsync().ConfigureAwait(false);
            var dbDrivers = await context.Drivers.Where(d => d.Status == DriverStatus.Active).OrderBy(d => d.LastName).ToListAsync().ConfigureAwait(false);
            var dbOrders = await context.Orders.Include(o => o.Customer).Where(o => o.Status == "New" && o.IsPosted).ToListAsync().ConfigureAwait(false);

            List<WaybillPoint> dbPoints = new();
            List<FuelTicket> dbTickets = new();

            if (waybill is not null)
            {
                _currentWaybill = waybill;

                var assignedDriver = await context.Drivers.FirstOrDefaultAsync(d => d.DriverID == waybill.DriverID).ConfigureAwait(false);
                if (assignedDriver is not null && !dbDrivers.Any(d => d.DriverID == assignedDriver.DriverID)) dbDrivers.Add(assignedDriver);

                var assignedVehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == waybill.VehicleID).ConfigureAwait(false);
                if (assignedVehicle is not null && !dbVehicles.Any(v => v.VehicleID == assignedVehicle.VehicleID)) dbVehicles.Add(assignedVehicle);

                dbPoints = await context.WaybillPoints.Include(wp => wp.Order).ThenInclude(o => o.Customer).Where(wp => wp.WaybillID == waybill.WaybillID).OrderBy(wp => wp.SequenceNumber).ToListAsync().ConfigureAwait(false);
                dbTickets = await context.FuelTickets.Where(t => t.WaybillID == waybill.WaybillID).OrderBy(t => t.TicketDate).ToListAsync().ConfigureAwait(false);
            }
            else
            {
                _currentWaybill = new Waybill { DateCreate = DateTime.Now, Status = WaybillStatus.Draft };
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableVehicles.Clear();
                foreach (var v in dbVehicles) AvailableVehicles.Add(v);

                AvailableDrivers.Clear();
                foreach (var d in dbDrivers) AvailableDrivers.Add(d);

                Points.Clear();
                foreach (var p in dbPoints) Points.Add(p);

                FuelTickets.Clear();
                foreach (var t in dbTickets) FuelTickets.Add(t);

                AvailableOrders.Clear();
                var existingOrderIds = Points.Select(p => p.OrderID).ToHashSet();
                foreach (var o in dbOrders)
                {
                    if (!existingOrderIds.Contains(o.OrderID)) AvailableOrders.Add(o);
                }

                if (waybill is not null)
                {
                    SelectedVehicle = AvailableVehicles.FirstOrDefault(v => v.VehicleID == waybill.VehicleID);
                    SelectedDriver = AvailableDrivers.FirstOrDefault(d => d.DriverID == waybill.DriverID);
                    Status = waybill.Status;
                    DateCreate = waybill.DateCreate;
                    DateOut = waybill.DateOut?.Date;
                    TimeOut = waybill.DateOut;
                    OdometerOut = waybill.OdometerOut;
                    FuelOut = waybill.FuelOut;
                    DateIn = waybill.DateIn?.Date;
                    TimeIn = waybill.DateIn;
                    OdometerIn = waybill.OdometerIn;
                    FuelIn = waybill.FuelIn;
                    TotalDistance = waybill.TotalDistance;
                    CalculatedFuelConsumption = waybill.CalculatedFuelConsumption;
                    Notes = waybill.Notes;
                    IsPosted = waybill.IsPosted;
                    DepartureTime = waybill.DepartureTime;
                    ExpectedArrivalTime = waybill.ExpectedArrivalTime;
                    ActualArrivalTime = waybill.ActualArrivalTime;
                }
                else
                {
                    Status = WaybillStatus.Draft;
                    DateCreate = DateTime.Now;
                    DateOut = DateTime.Today; TimeOut = DateTime.Now; DateIn = null; TimeIn = null;
                    OdometerOut = 0; FuelOut = 0; OdometerIn = 0; FuelIn = 0;
                    TotalDistance = 0; CalculatedFuelConsumption = 0; IsPosted = false;
                }

                CalculateLoad();
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }
}
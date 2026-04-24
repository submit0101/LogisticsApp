using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Wpf;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;

    [ObservableProperty] private int _activeRoutesCount;
    [ObservableProperty] private double _deliveredWeightToday;
    [ObservableProperty] private int _availableVehiclesCount;
    [ObservableProperty] private int _pendingOrdersCount;
    [ObservableProperty] private SeriesCollection _weightSeries = new();
    [ObservableProperty] private List<string> _dateLabels = new();
    [ObservableProperty] private Func<double, string> _weightFormatter = value => value.ToString("N1") + " т";
    [ObservableProperty] private SeriesCollection _vehicleStatusSeries = new();
    [ObservableProperty] private SeriesCollection _topCustomersSeries = new();
    [ObservableProperty] private List<string> _customerLabels = new();
    [ObservableProperty] private Func<double, string> _customerWeightFormatter = value => value.ToString("N0") + " кг";

    public DashboardViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        System.Windows.Application.Current.Dispatcher.InvokeAsync(LoadDataAsync);
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var today = DateTime.Today;

            ActiveRoutesCount = await context.Waybills.CountAsync(w => w.Status == WaybillStatus.Active);

            var deliveredOrdersToday = await context.Orders
                .Where(o => o.Status == "Delivered" && o.OrderDate >= today)
                .SumAsync(o => o.WeightKG);
            DeliveredWeightToday = Math.Round(deliveredOrdersToday / 1000.0, 2);

            var vehiclesOnRoute = await context.Waybills
                .Where(w => w.Status == WaybillStatus.Active)
                .Select(w => w.VehicleID)
                .Distinct()
                .ToListAsync();

            AvailableVehiclesCount = await context.Vehicles
                .CountAsync(v => v.Status == VehicleStatus.Active && !vehiclesOnRoute.Contains(v.VehicleID));

            PendingOrdersCount = await context.Orders.CountAsync(o => o.Status == "New");

            var totalVehicles = await context.Vehicles.CountAsync();
            var inactiveVehicles = await context.Vehicles.CountAsync(v => v.Status != VehicleStatus.Active);
            var activeOnRoute = vehiclesOnRoute.Count;
            var activeFree = totalVehicles - inactiveVehicles - activeOnRoute;

            VehicleStatusSeries = new SeriesCollection
            {
                new PieSeries { Title = "В рейсе", Values = new ChartValues<int> { activeOnRoute }, DataLabels = true },
                new PieSeries { Title = "Свободны", Values = new ChartValues<int> { activeFree }, DataLabels = true },
                new PieSeries { Title = "В архиве", Values = new ChartValues<int> { inactiveVehicles }, DataLabels = true }
            };
        }
        catch (Exception ex)
        {
            _notify.Error($"Сбой аналитики: {ex.Message}");
        }
    }
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LogisticsApp.Core;
using LogisticsApp.Messages;
using LogisticsApp.Models;
using LogisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LogisticsApp.ViewModels.Windows;

public sealed partial class RouteMapViewModel : ViewModelBase
{
    private readonly GeocodingService _geoService;
    private readonly NotificationService _notify;
    private readonly Waybill _currentWaybill;

    [ObservableProperty] private string _windowTitle = string.Empty;
    [ObservableProperty] private string _waybillTitle = string.Empty;
    [ObservableProperty] private string _vehicleInfo = string.Empty;
    [ObservableProperty] private string _driverName = string.Empty;
    [ObservableProperty] private double _totalDistanceKm;
    [ObservableProperty] private double _totalTimeMinutes;
    [ObservableProperty] private bool _isLoading;

    public event Action<string>? OnExecuteScript;

    public RouteMapViewModel(Waybill waybill, GeocodingService geoService, NotificationService notify)
    {
        _currentWaybill = waybill;
        _geoService = geoService;
        _notify = notify;

        WindowTitle = $"Интерактивная карта маршрута — Путевой лист №{_currentWaybill.WaybillID}";
        WaybillTitle = $"№ {_currentWaybill.WaybillID}";
        VehicleInfo = $"{_currentWaybill.Vehicle?.RegNumber} ({_currentWaybill.Vehicle?.Model})";
        DriverName = _currentWaybill.Driver?.FullName ?? "Не назначен";
    }

    public void DrawGeofence()
    {
        OnExecuteScript?.Invoke("drawGeofence(53.208101, 34.444738, 15000);");
    }

    public async Task DrawRouteAsync()
    {
        if (_currentWaybill.Points == null || !_currentWaybill.Points.Any())
        {
            _notify.Info("В этом путевом листе пока нет заказов.");
            return;
        }

        IsLoading = true;
        try
        {
            var points = new List<double[]>();
            var depot = _geoService.GetDepotLocation();
            points.Add(new double[] { depot.Lat, depot.Lng });

            var markerScript = string.Empty;
            markerScript += $"addMarker({depot.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {depot.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 'ДЕПО (Брянск, Московский пр., 103)', 'Black');";

            foreach (var point in _currentWaybill.Points.OrderBy(p => p.SequenceNumber))
            {
                double lat = point.Order?.Customer?.GeoLat ?? 0;
                double lon = point.Order?.Customer?.GeoLon ?? 0;

                if (lat == 0 && lon == 0)
                {
                    var address = point.Order?.Customer?.Address ?? "";
                    var coords = await _geoService.GetCoordinatesAsync(address);
                    if (coords.HasValue)
                    {
                        lat = coords.Value.Latitude;
                        lon = coords.Value.Longitude;
                    }
                }

                points.Add(new double[] { lat, lon });
                string tooltip = $"Точка {point.SequenceNumber}\\nЗаказ №{point.Order?.OrderID}\\nКлиент: {point.Order?.Customer?.Name}\\nАдрес: {point.Order?.Customer?.Address}";
                markerScript += $"addMarker({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{tooltip}', 'Red');";
            }

            points.Add(new double[] { depot.Lat, depot.Lng });

            var jsonPoints = JsonSerializer.Serialize(points);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                OnExecuteScript?.Invoke(markerScript);
                OnExecuteScript?.Invoke($"calculateRoute('{jsonPoints}', true, true);");
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void UpdateMetricsFromMap(double distanceKm, double timeMinutes, double timeInTrafficMinutes)
    {
        TotalDistanceKm = Math.Round(distanceKm, 2);
        TotalTimeMinutes = Math.Round(timeInTrafficMinutes > 0 ? timeInTrafficMinutes : timeMinutes, 0);
        WeakReferenceMessenger.Default.Send(new RouteInteractiveUpdateMessage(TotalDistanceKm, timeMinutes, timeInTrafficMinutes));
    }
}
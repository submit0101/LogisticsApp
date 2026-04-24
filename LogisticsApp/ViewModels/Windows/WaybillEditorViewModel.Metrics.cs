using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LogisticsApp.ViewModels.Windows;

public sealed partial class WaybillEditorViewModel
{
    partial void OnDateOutChanged(DateTime? value) => CalculateMetrics();
    partial void OnTimeOutChanged(DateTime? value) => CalculateMetrics();

    async partial void OnSelectedVehicleChanged(Vehicle? value)
    {
        CalculateLoad();
        CalculateMetrics();

        if (value != null)
        {
            var price = await _fuelPriceService.GetFuelPriceAsync(value.FuelType).ConfigureAwait(false);
            Application.Current.Dispatcher.Invoke(() => CurrentFuelPrice = price);
        }
    }

    partial void OnTotalDistanceChanged(double value) => CalculateMetrics();

    partial void OnNewTicketVolumeChanged(double value)
    {
        if (_isUpdatingTickets || CurrentFuelPrice <= 0) return;
        _isUpdatingTickets = true;
        NewTicketAmount = Math.Round((decimal)value * CurrentFuelPrice, 2);
        _isUpdatingTickets = false;
    }

    partial void OnNewTicketAmountChanged(decimal value)
    {
        if (_isUpdatingTickets || CurrentFuelPrice <= 0) return;
        _isUpdatingTickets = true;
        NewTicketVolume = Math.Round((double)(value / CurrentFuelPrice), 2);
        _isUpdatingTickets = false;
    }

    private void CalculateLoad()
    {
        try
        {
            CurrentTotalWeight = Points.Sum(p => p.Order?.WeightKG ?? 0);
            CurrentTotalVolume = Math.Round(CurrentTotalWeight / 350.0, 1);

            if (SelectedVehicle is not null)
            {
                double overloadFactor = 1.0;
                if (_currentSettings != null) overloadFactor += (_currentSettings.MaxOverloadPercentage / 100.0);

                IsWeightOverload = SelectedVehicle.CapacityKG > 0 && CurrentTotalWeight > SelectedVehicle.CapacityKG * overloadFactor;
                IsVolumeOverload = SelectedVehicle.CapacityM3 > 0 && CurrentTotalVolume > SelectedVehicle.CapacityM3;
            }
            else
            {
                IsWeightOverload = false;
                IsVolumeOverload = false;
            }

            CalculateMetrics();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    private void CalculateExpectedTime()
    {
        DateTime baseTime = DepartureTime ?? (DateOut.HasValue && TimeOut.HasValue ? DateOut.Value.Date + TimeOut.Value.TimeOfDay : DateTime.Now);

        double totalDriveTimeMinutes = RealDriveTimeMinutes > 0 ? RealDriveTimeMinutes : (TotalDistance / 60.0) * 60;
        int unloadingTimeMinutes = Points.Count * 30;
        int totalExpectedMinutes = (int)Math.Round(totalDriveTimeMinutes) + unloadingTimeMinutes + DelayMinutes;

        ExpectedArrivalTime = baseTime.AddMinutes(totalExpectedMinutes);

        if (Status != WaybillStatus.Completed && Status != WaybillStatus.Cancelled)
        {
            DateIn = ExpectedArrivalTime.Value.Date;
            TimeIn = ExpectedArrivalTime;
        }
    }

    private void CalculateMetrics()
    {
        if (SelectedVehicle == null) return;

        OdometerOut = SelectedVehicle.Mileage;
        FuelOut = SelectedVehicle.CurrentFuelLevel;
        OdometerIn = OdometerOut + Math.Round(TotalDistance);

        bool isWinter = DateCreate.Month >= 11 || DateCreate.Month <= 3;
        double winterCoeff = isWinter ? (1.0 + (SelectedVehicle.WinterFuelBonus / 100.0)) : 1.0;

        double baseCons = (TotalDistance / 100.0) * SelectedVehicle.BaseFuelConsumption;
        double cargoCons = (TotalDistance / 100.0) * (CurrentTotalWeight / 1000.0) * SelectedVehicle.CargoFuelBonus;

        CalculatedFuelConsumption = Math.Round((baseCons + cargoCons) * winterCoeff, 2);

        double ticketsSum = FuelTickets.Sum(t => t.VolumeLiters);
        FuelIn = Math.Round(FuelOut.Value - CalculatedFuelConsumption + ticketsSum, 2);

        CalculateExpectedTime();
    }

    private async Task RecalculateRouteMetricsAsync()
    {
        var currentPoints = new List<WaybillPoint>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Points.Count == 0)
            {
                TotalDistance = 0;
                RealDriveTimeMinutes = 0;
                CalculateMetrics();
            }
            currentPoints = Points.OrderBy(p => p.SequenceNumber).ToList();
        });

        if (currentPoints.Count == 0) return;

        var depot = _geoService.GetDepotLocation();
        var locations = new List<GeoPoint> { depot };

        foreach (var point in currentPoints)
        {
            double lat = point.Order?.Customer?.GeoLat ?? 0;
            double lon = point.Order?.Customer?.GeoLon ?? 0;

            if (lat == 0 || lon == 0)
            {
                var address = point.Order?.Customer?.Address ?? string.Empty;
                try
                {
                    var coords = await _geoService.GetCoordinatesAsync(address).ConfigureAwait(false);
                    if (coords.HasValue)
                    {
                        lat = coords.Value.Latitude;
                        lon = coords.Value.Longitude;

                        if (point.Order?.Customer != null)
                        {
                            point.Order.Customer.GeoLat = lat;
                            point.Order.Customer.GeoLon = lon;
                        }
                    }
                }
                catch { }
            }
            locations.Add(new GeoPoint(lat, lon));
        }

        locations.Add(depot);

        try
        {
            var result = await _routeService.CalculateRouteAsync(locations).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalDistance = Math.Round(result.TotalDistanceKm, 2);
                RealDriveTimeMinutes = result.EstimatedTime.TotalMinutes;
                CalculateMetrics();

                if (result.IsMathFallback)
                {
                    _notify.Warning("Расчет произведен математически. Yandex API недоступен.");
                }
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Error($"Сбой расчета: {ex.Message}");
                CalculateFallbackDistance();
            });
        }
    }
}
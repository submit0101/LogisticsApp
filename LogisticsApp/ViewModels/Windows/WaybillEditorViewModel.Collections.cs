using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.ViewModels.Windows;

public sealed partial class WaybillEditorViewModel
{
    [RelayCommand]
    private async Task AddPointAsync()
    {
        if (SelectedOrderToAdd is not null)
        {
            var orderToAdd = SelectedOrderToAdd;
            Points.Add(new WaybillPoint { Order = orderToAdd, OrderID = orderToAdd.OrderID, SequenceNumber = Points.Count + 1, Status = WaybillPointStatus.Pending, DeliveredWeightKG = null });
            AvailableOrders.Remove(orderToAdd);
            SelectedOrderToAdd = null;

            UpdatePointSequence();
            CalculateLoad();
            OptimizeRouteCommand.NotifyCanExecuteChanged();

            IsLoading = true;
            try
            {
                await RecalculateRouteMetricsAsync().ConfigureAwait(false);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemovePointAsync()
    {
        if (SelectedPoint is not null)
        {
            var pointToRemove = SelectedPoint;
            if (pointToRemove.Order is not null) AvailableOrders.Add(pointToRemove.Order);
            Points.Remove(pointToRemove);

            UpdatePointSequence();
            CalculateLoad();
            OptimizeRouteCommand.NotifyCanExecuteChanged();

            IsLoading = true;
            try
            {
                await RecalculateRouteMetricsAsync().ConfigureAwait(false);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanMovePointUp))]
    private async Task MovePointUpAsync()
    {
        if (SelectedPoint == null) return;
        int index = Points.IndexOf(SelectedPoint);

        if (index > 0)
        {
            Points.Move(index, index - 1);
            UpdatePointSequence();

            IsLoading = true;
            try
            {
                await RecalculateRouteMetricsAsync().ConfigureAwait(false);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private bool CanMovePointUp() => SelectedPoint != null && Points.IndexOf(SelectedPoint) > 0 && IsEditable;

    [RelayCommand(CanExecute = nameof(CanMovePointDown))]
    private async Task MovePointDownAsync()
    {
        if (SelectedPoint == null) return;
        int index = Points.IndexOf(SelectedPoint);

        if (index >= 0 && index < Points.Count - 1)
        {
            Points.Move(index, index + 1);
            UpdatePointSequence();

            IsLoading = true;
            try
            {
                await RecalculateRouteMetricsAsync().ConfigureAwait(false);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private bool CanMovePointDown() => SelectedPoint != null && Points.IndexOf(SelectedPoint) >= 0 && Points.IndexOf(SelectedPoint) < Points.Count - 1 && IsEditable;

    private void UpdatePointSequence()
    {
        for (int i = 0; i < Points.Count; i++)
        {
            Points[i].SequenceNumber = i + 1;
        }
        MovePointUpCommand.NotifyCanExecuteChanged();
        MovePointDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddFuelTicket()
    {
        if (NewTicketVolume <= 0 || NewTicketAmount <= 0 || string.IsNullOrWhiteSpace(NewTicketNumber) || SelectedVehicle == null)
        {
            _notify.Warning("Пожалуйста, корректно заполните все поля чека АЗС.");
            return;
        }

        if (SelectedVehicle.FuelCapacity > 0 && FuelOut + FuelTickets.Sum(t => t.VolumeLiters) + NewTicketVolume > SelectedVehicle.FuelCapacity)
        {
            _notify.Error("Блокировка: Объем заправляемого топлива превышает вместимость бака ТС.");
            return;
        }

        FuelTickets.Add(new FuelTicket
        {
            TicketDate = NewTicketDate,
            VolumeLiters = NewTicketVolume,
            Amount = NewTicketAmount,
            TicketNumber = NewTicketNumber,
            FuelType = SelectedVehicle.FuelType,
            PricePerLiter = CurrentFuelPrice
        });

        NewTicketVolume = 0;
        NewTicketAmount = 0;
        NewTicketNumber = string.Empty;

        CalculateMetrics();
    }

    [RelayCommand]
    private void RemoveFuelTicket()
    {
        if (SelectedFuelTicket != null)
        {
            FuelTickets.Remove(SelectedFuelTicket);
            CalculateMetrics();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOptimize))]
    private async Task OptimizeRouteAsync()
    {
        IsLoading = true;
        try
        {
            var depotLocation = _geoService.GetDepotLocation();
            var locations = new List<GeoPoint> { depotLocation };

            var currentPoints = new List<WaybillPoint>();
            Application.Current.Dispatcher.Invoke(() => currentPoints = Points.ToList());

            foreach (var point in currentPoints)
            {
                double lat = point.Order?.Customer?.GeoLat ?? 0;
                double lon = point.Order?.Customer?.GeoLon ?? 0;

                if (lat == 0 || lon == 0)
                {
                    var address = point.Order?.Customer?.Address ?? string.Empty;
                    var coords = await _geoService.GetCoordinatesAsync(address).ConfigureAwait(false);

                    if (coords.HasValue)
                    {
                        lat = coords.Value.Latitude;
                        lon = coords.Value.Longitude;
                    }
                    else
                    {
                        lat = depotLocation.Lat;
                        lon = depotLocation.Lng;
                    }
                }
                locations.Add(new GeoPoint(lat, lon));
            }
            locations.Add(depotLocation);

            var result = await _routeService.CalculateRouteAsync(locations).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalDistance = Math.Round(result.TotalDistanceKm, 2);
                RealDriveTimeMinutes = result.EstimatedTime.TotalMinutes;
                CalculateMetrics();

                if (result.IsMathFallback)
                {
                    _notify.Warning("Оптимизация не удалась. Расчет произведен математически.");
                }
                else
                {
                    _notify.Success($"Оптимизация завершена. Дистанция: {TotalDistance} км.");
                }
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Error($"Ошибка маршрутизации: {ex.Message}");
                CalculateFallbackDistance();
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanOptimize() => Points.Count > 1;
}
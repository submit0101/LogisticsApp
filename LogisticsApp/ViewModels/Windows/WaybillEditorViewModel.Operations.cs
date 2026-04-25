using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core.Specifications;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.ViewModels.Windows;

public sealed partial class WaybillEditorViewModel
{
    partial void OnStatusChanged(WaybillStatus value)
    {
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsInTransit));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsEditable));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEditable));
    partial void OnIsPostedChanged(bool value) => OnPropertyChanged(nameof(IsEditable));

    [RelayCommand]
    private async Task ApproveRouteAsync()
    {
        ValidateAllProperties();
        if (HasErrors || SelectedVehicle == null || SelectedDriver == null) return;

        if (TotalDistance <= 0)
        {
            CalculateFallbackDistance();

            if (TotalDistance <= 0)
            {
                _notify.Error("БЛОКИРОВКА: Невозможно провести рейс. У контрагентов нет GPS координат. Пожалуйста, введите дистанцию вручную в поле километража!");
                return;
            }
        }

        var prevStatus = Status;
        Status = WaybillStatus.Planned;

        bool success = await SaveInternalAsync(true, suppressSuccessNotification: true, closeWindow: false);
        if (success)
        {
            _notify.Success("Маршрут утвержден. Статус изменен на 'К выполнению'.");
        }
        else
        {
            Status = prevStatus;
        }
    }

    [RelayCommand]
    private async Task StartRouteAsync()
    {
        if (TotalDistance <= 0)
        {
            CalculateFallbackDistance();
            if (TotalDistance <= 0)
            {
                _notify.Error("БЛОКИРОВКА: Дистанция равна нулю! Введите дистанцию вручную.");
                return;
            }
        }

        var prevStatus = Status;
        var prevDeparture = DepartureTime;
        var prevDateOut = DateOut;
        var prevTimeOut = TimeOut;

        Status = WaybillStatus.Active;
        DepartureTime = DateTime.Now;
        DateOut = DepartureTime.Value.Date;
        TimeOut = DepartureTime;

        CalculateMetrics();

        bool success = await SaveInternalAsync(true, suppressSuccessNotification: true, closeWindow: false);
        if (success)
        {
            _notify.Success($"Рейс начат. Расчетное время возвращения: {ExpectedArrivalTime:HH:mm}");
        }
        else
        {
            Status = prevStatus;
            DepartureTime = prevDeparture;
            DateOut = prevDateOut;
            TimeOut = prevTimeOut;
            CalculateMetrics();
        }
    }

    [RelayCommand]
    private async Task FinishRouteAsync()
    {
        bool allPointsProcessed = Points.Count > 0 && Points.All(p => p.Status != WaybillPointStatus.Pending);

        if (!allPointsProcessed)
        {
            _notify.Error("БЛОКИРОВКА: Невозможно завершить путевой лист. Проставьте статусы (Доставлено/Отказ) для всех точек маршрута.");
            return;
        }

        foreach (var p in Points)
        {
            if (p.Status == WaybillPointStatus.PartiallyDelivered && (p.DeliveredWeightKG == null || p.DeliveredWeightKG <= 0 || p.DeliveredWeightKG >= p.Order?.WeightKG))
            {
                _notify.Error($"БЛОКИРОВКА: Укажите корректный фактический вес для частично доставленного заказа №{p.OrderID}. Вес должен быть больше 0 и меньше расчетного ({p.Order?.WeightKG} кг).");
                return;
            }
        }

        var prevStatus = Status;
        var prevActual = ActualArrivalTime;
        var prevDateIn = DateIn;
        var prevTimeIn = TimeIn;

        Status = WaybillStatus.Completed;
        ActualArrivalTime = DateTime.Now;
        DateIn = ActualArrivalTime.Value.Date;
        TimeIn = ActualArrivalTime;

        // При завершении рейса окно обычно закрывается
        bool success = await SaveInternalAsync(true, suppressSuccessNotification: true, closeWindow: true);
        if (success)
        {
            _notify.Success("Рейс успешно завершен!");
        }
        else
        {
            Status = prevStatus;
            ActualArrivalTime = prevActual;
            DateIn = prevDateIn;
            TimeIn = prevTimeIn;
        }
    }

    [RelayCommand] private async Task SaveDraftAsync() => await SaveInternalAsync(false, false, true);

    [RelayCommand]
    private async Task UnpostAsync()
    {
        IsLoading = true;
        await Task.Delay(800); // Плавная анимация загрузки
        try
        {
            await _dispatchService.UnpostWaybillAsync(_currentWaybill).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                _currentWaybill.IsPosted = false;
                IsPosted = false;
                Status = WaybillStatus.Draft;
                DepartureTime = null;
                ExpectedArrivalTime = null;
                _notify.Success("Проведение отменено. Метрики ТС и остатки восстановлены.");
            });
        }
        catch (InvalidOperationException ex)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Error($"Ошибка БД: {ex.Message}"));
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    private async Task<bool> SaveInternalAsync(bool postDocument, bool suppressSuccessNotification = false, bool closeWindow = true)
    {
        ValidateAllProperties();

        if (HasErrors || SelectedVehicle is null || SelectedDriver is null)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Warning("Проверьте правильность заполнения полей"));
            return false;
        }

        Application.Current.Dispatcher.Invoke(() => CalculateMetrics());

        if (postDocument && Status != WaybillStatus.Draft && Status != WaybillStatus.Cancelled)
        {
            double totalFuelAvailable = (FuelOut ?? 0) + FuelTickets.Sum(t => t.VolumeLiters);
            double requiredFuelWithReserve = CalculatedFuelConsumption * 1.05;

            if (totalFuelAvailable < requiredFuelWithReserve)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error($"БЛОКИРОВКА ПРОВЕДЕНИЯ: Недостаточно ГСМ на рейс!\n\nТребуется с учетом 5% резерва: {requiredFuelWithReserve:F1} л.\nВ баке при выезде: {(FuelOut ?? 0):F1} л.\nЗапланировано заправок: {FuelTickets.Sum(t => t.VolumeLiters):F1} л.\n\nДефицит: {(requiredFuelWithReserve - totalFuelAvailable):F1} л. Добавьте плановую заправку."));
                return false;
            }

            if (SelectedVehicle.FuelCapacity > 0)
            {
                if ((FuelOut ?? 0) > SelectedVehicle.FuelCapacity)
                {
                    Application.Current.Dispatcher.Invoke(() => _notify.Error($"БЛОКИРОВКА ПРОВЕДЕНИЯ: Остаток при выезде ({(FuelOut ?? 0):F1} л) физически превышает объем бака тягача ({SelectedVehicle.FuelCapacity:F1} л)."));
                    return false;
                }

                if ((FuelIn ?? 0) > SelectedVehicle.FuelCapacity)
                {
                    Application.Current.Dispatcher.Invoke(() => _notify.Error($"БЛОКИРОВКА ПРОВЕДЕНИЯ: Расчетный остаток по возвращении ({(FuelIn ?? 0):F1} л) превышает объем бака ({SelectedVehicle.FuelCapacity:F1} л). Ошибка в планировании заправок."));
                    return false;
                }

                foreach (var ticket in FuelTickets)
                {
                    if (ticket.VolumeLiters > SelectedVehicle.FuelCapacity)
                    {
                        Application.Current.Dispatcher.Invoke(() => _notify.Error($"БЛОКИРОВКА ПРОВЕДЕНИЯ: Объем дозаправки по чеку {ticket.TicketNumber} ({ticket.VolumeLiters:F1} л) превышает максимальный объем бака ({SelectedVehicle.FuelCapacity:F1} л)."));
                        return false;
                    }
                }
            }
        }

        IsLoading = true;
        await Task.Delay(800); // Плавная анимация интерфейса

        _currentWaybill.Status = Status;
        _currentWaybill.AssignTransportAndDriver(SelectedVehicle.VehicleID, SelectedDriver.DriverID);

        DateTime? finalDateOut = DateOut.HasValue ? DateOut.Value.Date + (TimeOut?.TimeOfDay ?? TimeSpan.Zero) : null;
        DateTime? finalDateIn = DateIn.HasValue ? DateIn.Value.Date + (TimeIn?.TimeOfDay ?? TimeSpan.Zero) : null;

        _currentWaybill.DateOut = finalDateOut;
        _currentWaybill.DateIn = finalDateIn;

        _currentWaybill.OdometerOut = OdometerOut.HasValue ? (int)Math.Round(OdometerOut.Value) : null;
        _currentWaybill.FuelOut = FuelOut;
        _currentWaybill.OdometerIn = OdometerIn.HasValue ? (int)Math.Round(OdometerIn.Value) : null;
        _currentWaybill.FuelIn = FuelIn;

        _currentWaybill.TotalDistance = TotalDistance;
        _currentWaybill.CalculatedFuelConsumption = CalculatedFuelConsumption;
        _currentWaybill.Notes = Notes;
        _currentWaybill.IsPosted = postDocument;
        _currentWaybill.DepartureTime = DepartureTime;
        _currentWaybill.ExpectedArrivalTime = ExpectedArrivalTime;
        _currentWaybill.ActualArrivalTime = ActualArrivalTime;

        _currentWaybill.Points = Points.Select(p => new WaybillPoint { WP_ID = p.WP_ID, OrderID = p.OrderID, SequenceNumber = p.SequenceNumber, Status = p.Status, DeliveredWeightKG = p.DeliveredWeightKG, Waybill = _currentWaybill }).ToList();
        _currentWaybill.FuelTickets = FuelTickets.Select(t => new FuelTicket { TicketID = t.TicketID, TicketDate = t.TicketDate, VolumeLiters = t.VolumeLiters, Amount = t.Amount, TicketNumber = t.TicketNumber, FuelType = t.FuelType, PricePerLiter = t.PricePerLiter, Waybill = _currentWaybill }).ToList();

        try
        {
            var validationResult = await _tripValidationService.ValidateTripAsync(_currentWaybill).ConfigureAwait(false);

            if (postDocument && !validationResult.IsValid)
            {
                var errors = string.Join("\n", validationResult.Messages.Where(m => m.Level == SpecificationLevel.Error).Select(m => m.Message));
                Application.Current.Dispatcher.Invoke(() => _notify.Error($"БЛОКИРОВКА ПРОВЕДЕНИЯ:\n{errors}"));
                return false;
            }

            var warnings = validationResult.Messages.Where(m => m.Level == SpecificationLevel.Warning).ToList();
            if (warnings.Any())
            {
                var warnMsgs = string.Join("\n\n• ", warnings.Select(m => m.Message));
                bool proceed = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    proceed = MessageBox.Show($"Система выявила следующие предупреждения:\n\n• {warnMsgs}\n\nВы уверены, что хотите сохранить рейс?", "Контроль диспетчеризации", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
                });
                if (!proceed) return false;
            }

            if (postDocument && IsVolumeOverload)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error("БЛОКИРОВКА ПРОВЕДЕНИЯ: Выявлен критический перегруз по ОБЪЕМУ кузова."));
                return false;
            }
            else if (!postDocument && IsVolumeOverload)
            {
                bool proceed = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    proceed = MessageBox.Show("Обнаружен перегруз по объему! Сохранить черновик?", "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
                });
                if (!proceed) return false;
            }

            await _dispatchService.SaveWaybillAsync(_currentWaybill, postDocument, SelectedVehicle).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!suppressSuccessNotification)
                {
                    _notify.Success(postDocument ? "Путевой лист сохранен, метрики зафиксированы" : "Черновик сохранен");
                }

                if (closeWindow)
                {
                    RequestClose?.Invoke(true);
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Error($"Сбой БД: {ex.Message}"));
            return false;
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    private void CalculateFallbackDistance()
    {
        if (Points.Count == 0) return;

        double distance = 0;
        var depot = _geoService.GetDepotLocation();
        var prevPoint = depot;
        bool hasMissingCoords = false;

        foreach (var wp in Points.OrderBy(p => p.SequenceNumber))
        {
            double lat = wp.Order?.Customer?.GeoLat ?? 0;
            double lon = wp.Order?.Customer?.GeoLon ?? 0;

            if (lat == 0 || lon == 0)
            {
                hasMissingCoords = true;
                continue;
            }

            distance += CalculateHaversine(prevPoint.Lat, prevPoint.Lng, lat, lon);
            prevPoint = new GeoPoint(lat, lon);
        }

        if (distance > 0 || !hasMissingCoords)
        {
            distance += CalculateHaversine(prevPoint.Lat, prevPoint.Lng, depot.Lat, depot.Lng);
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            double calculated = Math.Round(distance * 1.3, 2);

            if (calculated > 0)
            {
                TotalDistance = calculated;
                RealDriveTimeMinutes = Math.Round((TotalDistance / 40.0) * 60, 0);
            }
            else if (hasMissingCoords && TotalDistance <= 0)
            {
                _notify.Warning("ВНИМАНИЕ: Нет GPS-координат. Пожалуйста, введите дистанцию вручную.");
            }

            CalculateMetrics();
        });
    }

    private static double CalculateHaversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace LogisticsApp.ViewModels.Windows;

public partial class VehicleEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IDialogService _dialogService;
    private readonly OverlayService _overlay;

    private Vehicle _currentVehicle = new();
    private bool _isNew;

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Гос. номер обязателен")]
    [RegularExpression(@"^[A-ZА-Я0-9]{5,10}$", ErrorMessage = "Некорректный формат гос. номера")]
    private string _regNumber = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(17, MinimumLength = 17, ErrorMessage = "VIN номер должен содержать ровно 17 символов")]
    private string _vIN = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Модель обязательна")]
    private string _model = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1900, 2100, ErrorMessage = "Год выпуска должен быть реальным")]
    private int _year = DateTime.Today.Year;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(10, 50000, ErrorMessage = "Грузоподъемность от 10 до 50000 кг")]
    private int _capacityKG;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 200.0, ErrorMessage = "Объем от 1 до 200 куб.м")]
    private double _capacityM3;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 5000000, ErrorMessage = "Некорректный пробег")]
    private int _mileage;

    [ObservableProperty] private bool _isFridge;
    [ObservableProperty] private DateTime _sanitizationDate = DateTime.Today;
    [ObservableProperty] private VehicleStatus _status = VehicleStatus.Active;
    [ObservableProperty] private FuelType _fuelType = FuelType.DT;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 2000.0, ErrorMessage = "Укажите корректный остаток")]
    private double _currentFuelLevel;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1.0, 100.0, ErrorMessage = "Базовая норма от 1 до 100 л")]
    private double _baseFuelConsumption;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 50.0, ErrorMessage = "Надбавка от 0 до 50 л")]
    private double _cargoFuelBonus;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 50.0, ErrorMessage = "Зимняя надбавка от 0 до 50%")]
    private double _winterFuelBonus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditServiceRecordCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteServiceRecordCommand))]
    private VehicleServiceRecord? _selectedServiceRecord;

    public ObservableCollection<VehicleServiceRecord> ServiceRecords { get; } = new();

    public Array AvailableStatuses => Enum.GetValues(typeof(VehicleStatus));
    public Array AvailableFuelTypes => Enum.GetValues(typeof(FuelType));

    public VehicleEditorViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        IDialogService dialogService,
        OverlayService overlay)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _dialogService = dialogService;
        _overlay = overlay;
    }

    public void Initialize(Vehicle? vehicle)
    {
        _isNew = vehicle == null;
        _currentVehicle = vehicle ?? new Vehicle();

        if (!_isNew)
        {
            RegNumber = _currentVehicle.RegNumber;
            Model = _currentVehicle.Model;
            VIN = _currentVehicle.VIN ?? string.Empty;
            Year = _currentVehicle.Year == 0 ? DateTime.Today.Year : _currentVehicle.Year;
            CapacityKG = _currentVehicle.CapacityKG;
            CapacityM3 = _currentVehicle.CapacityM3;
            Mileage = _currentVehicle.Mileage;
            IsFridge = _currentVehicle.IsFridge;
            SanitizationDate = _currentVehicle.SanitizationDate == DateTime.MinValue ? DateTime.Today : _currentVehicle.SanitizationDate;
            Status = _currentVehicle.Status;
            FuelType = _currentVehicle.FuelType;
            CurrentFuelLevel = _currentVehicle.CurrentFuelLevel;
            BaseFuelConsumption = _currentVehicle.BaseFuelConsumption;
            CargoFuelBonus = _currentVehicle.CargoFuelBonus;
            WinterFuelBonus = _currentVehicle.WinterFuelBonus;

            _ = LoadServiceRecordsAsync();
        }
        else
        {
            CapacityKG = 1500;
            CapacityM3 = 10.0;
            Year = DateTime.Today.Year;
            SanitizationDate = DateTime.Today;
            FuelType = FuelType.DT;
            BaseFuelConsumption = 12.0;
            CargoFuelBonus = 1.3;
            WinterFuelBonus = 10.0;
        }

        ValidateAllProperties();
    }

    private async Task LoadServiceRecordsAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var records = await context.VehicleServiceRecords
                .AsNoTracking()
                .Where(r => r.VehicleID == _currentVehicle.VehicleID)
                .OrderByDescending(r => r.ServiceDate)
                .ToListAsync();

            ServiceRecords.Clear();
            foreach (var r in records) ServiceRecords.Add(r);
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

    [RelayCommand]
    private void AddServiceRecord()
    {
        if (_dialogService.ShowVehicleServiceRecordEditor(out var newRecord, null, Mileage))
        {
            ServiceRecords.Insert(0, newRecord!);
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyRecord))]
    private void EditServiceRecord()
    {
        if (SelectedServiceRecord != null && _dialogService.ShowVehicleServiceRecordEditor(out var updatedRecord, SelectedServiceRecord, Mileage))
        {
            var index = ServiceRecords.IndexOf(SelectedServiceRecord);
            if (index >= 0 && updatedRecord != null)
            {
                ServiceRecords[index] = updatedRecord;
                SelectedServiceRecord = updatedRecord;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyRecord))]
    private void DeleteServiceRecord()
    {
        if (SelectedServiceRecord != null) ServiceRecords.Remove(SelectedServiceRecord);
    }

    private bool CanModifyRecord() => SelectedServiceRecord != null;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            _notify.Warning("Пожалуйста, исправьте ошибки валидации.");
            return;
        }

        IsLoading = true;

        _currentVehicle.RegNumber = RegNumber;
        _currentVehicle.Model = Model;
        _currentVehicle.VIN = VIN;
        _currentVehicle.Year = Year;
        _currentVehicle.CapacityKG = CapacityKG;
        _currentVehicle.CapacityM3 = CapacityM3;
        _currentVehicle.Mileage = Mileage;
        _currentVehicle.IsFridge = IsFridge;
        _currentVehicle.SanitizationDate = SanitizationDate;
        _currentVehicle.Status = Status;
        _currentVehicle.FuelType = FuelType;
        _currentVehicle.CurrentFuelLevel = CurrentFuelLevel;
        _currentVehicle.BaseFuelConsumption = BaseFuelConsumption;
        _currentVehicle.CargoFuelBonus = CargoFuelBonus;
        _currentVehicle.WinterFuelBonus = WinterFuelBonus;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            if (_isNew)
            {
                _currentVehicle.ServiceRecords = ServiceRecords.ToList();
                context.Vehicles.Add(_currentVehicle);
            }
            else
            {
                var vehicleToUpdate = await context.Vehicles.Include(v => v.ServiceRecords).FirstOrDefaultAsync(v => v.VehicleID == _currentVehicle.VehicleID);
                if (vehicleToUpdate == null) return;

                context.Entry(vehicleToUpdate).OriginalValues["RowVersion"] = _currentVehicle.RowVersion;
                context.Entry(vehicleToUpdate).CurrentValues.SetValues(_currentVehicle);

                var existingRecordIds = ServiceRecords.Where(r => r.RecordID != 0).Select(r => r.RecordID).ToList();
                var recordsToRemove = vehicleToUpdate.ServiceRecords.Where(r => !existingRecordIds.Contains(r.RecordID)).ToList();

                foreach (var r in recordsToRemove) context.VehicleServiceRecords.Remove(r);

                foreach (var r in ServiceRecords)
                {
                    if (r.RecordID == 0)
                    {
                        r.VehicleID = vehicleToUpdate.VehicleID;
                        context.VehicleServiceRecords.Add(r);
                    }
                    else
                    {
                        var existing = vehicleToUpdate.ServiceRecords.First(er => er.RecordID == r.RecordID);
                        context.Entry(existing).CurrentValues.SetValues(r);
                    }
                }
            }

            await context.SaveChangesAsync();
            _notify.Success("Транспортное средство сохранено");
            RequestClose?.Invoke(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            _notify.Error("Обнаружен конфликт версий. Данные были изменены другим пользователем.");
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

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
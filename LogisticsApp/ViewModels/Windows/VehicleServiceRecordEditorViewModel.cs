using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using Microsoft.Extensions.Hosting;
using System;
using System.ComponentModel.DataAnnotations;

namespace LogisticsApp.ViewModels.Windows;

public partial class VehicleServiceRecordEditorViewModel : ViewModelBase
{
    private VehicleServiceRecord _currentRecord = new();

    public event Action<bool>? RequestClose;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Укажите дату обслуживания")]
    private DateTime _serviceDate = DateTime.Today;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Выберите тип обслуживания")]
    private VehicleServiceType _serviceType = VehicleServiceType.RoutineMaintenance;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Описание обязательно")]
    [MinLength(3, ErrorMessage = "Минимум 3 символа")]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 10000000.0, ErrorMessage = "Некорректная стоимость")]
    private decimal _cost;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 5000000, ErrorMessage = "Некорректный пробег")]
    private int _odometerReading;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Укажите механика или название СТО")]
    private string _mechanicName = string.Empty;

    public Array AvailableServiceTypes => Enum.GetValues(typeof(VehicleServiceType));

    public void Initialize(VehicleServiceRecord? record, int currentOdometer)
    {
        if (record != null)
        {
            _currentRecord = new VehicleServiceRecord
            {
                RecordID = record.RecordID,
                VehicleID = record.VehicleID,
                ServiceDate = record.ServiceDate,
                ServiceType = record.ServiceType,
                Description = record.Description,
                Cost = record.Cost,
                OdometerReading = record.OdometerReading,
                MechanicName = record.MechanicName,
                RowVersion = record.RowVersion
            };

            ServiceDate = record.ServiceDate;
            ServiceType = record.ServiceType;
            Description = record.Description;
            Cost = record.Cost;
            OdometerReading = record.OdometerReading;
            MechanicName = record.MechanicName;
        }
        else
        {
            _currentRecord = new VehicleServiceRecord();
            OdometerReading = currentOdometer;
        }

        ValidateAllProperties();
    }

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        _currentRecord.ServiceDate = ServiceDate;
        _currentRecord.ServiceType = ServiceType;
        _currentRecord.Description = Description;
        _currentRecord.Cost = Cost;
        _currentRecord.OdometerReading = OdometerReading;
        _currentRecord.MechanicName = MechanicName;

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    public VehicleServiceRecord GetRecord() => _currentRecord;
}
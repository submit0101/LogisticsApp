using System;
using System.ComponentModel;

namespace LogisticsApp.Models.DTOs.Reports;

public class TonKilometerReportDto
{
    [DisplayName("Модель ТС")]
    public string VehicleName { get; set; } = string.Empty;

    [DisplayName("ФИО Водителя")]
    public string DriverName { get; set; } = string.Empty;

    [DisplayName("Дата выезда")]
    public DateTime DateOut { get; set; }

    [DisplayName("Дата возврата")]
    public DateTime DateIn { get; set; }

    [DisplayName("Пробег (км)")]
    public decimal TotalMileage { get; set; }

    [DisplayName("С грузом (км)")]
    public decimal LoadedMileage { get; set; }

    [DisplayName("Вес груза (т)")]
    public decimal TotalWeightTons { get; set; }

    [DisplayName("Тн * км")]
    public decimal TonKilometers { get; set; }

    [DisplayName("Время в наряде (ч)")]
    public decimal WorkHours { get; set; }

    [DisplayName("Отопитель (ч)")]
    public decimal HeaterHours { get; set; }

    [DisplayName("Рефрижератор (ч)")]
    public decimal RefrigeratorHours { get; set; }

    [DisplayName("Заправлено (л)")]
    public decimal FuelRefueled { get; set; }
}

public class MileageReportDto
{
    [DisplayName("Гос. номер")]
    public string VehicleRegNumber { get; set; } = string.Empty;

    [DisplayName("Модель ТС")]
    public string VehicleModel { get; set; } = string.Empty;

    [DisplayName("Пробег за период (км)")]
    public decimal PeriodMileage { get; set; }

    [DisplayName("Общий пробег с начала экспл. (км)")]
    public decimal TotalMileageSinceStart { get; set; }
}

public class OdometerReportDto
{
    [DisplayName("Гос. номер")]
    public string VehicleRegNumber { get; set; } = string.Empty;

    [DisplayName("Модель ТС")]
    public string VehicleModel { get; set; } = string.Empty;

    [DisplayName("Одометр (Начало периода)")]
    public decimal OdometerStart { get; set; }

    [DisplayName("Пробег за период (км)")]
    public decimal PeriodMileage { get; set; }

    [DisplayName("Одометр (Конец периода)")]
    public decimal OdometerEnd { get; set; }

    [DisplayName("Общий пробег с начала экспл. (км)")]
    public decimal TotalMileageSinceStart { get; set; }
}
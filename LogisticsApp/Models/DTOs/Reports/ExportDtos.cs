using System;
using System.ComponentModel;

namespace LogisticsApp.Models.DTOs;

public class VehicleExportDto
{
    [DisplayName("Гос. номер")]
    public string RegNumber { get; set; } = string.Empty;

    [DisplayName("Марка и Модель")]
    public string Model { get; set; } = string.Empty;

    [DisplayName("VIN номер")]
    public string VIN { get; set; } = string.Empty;

    [DisplayName("Год выпуска")]
    public int Year { get; set; }

    [DisplayName("Грузоподъемность (кг)")]
    public int CapacityKG { get; set; }

    [DisplayName("Объем кузова (м3)")]
    public double CapacityM3 { get; set; }

    [DisplayName("Пробег (км)")]
    public int Mileage { get; set; }

    [DisplayName("Рефрижератор")]
    public string IsFridge { get; set; } = string.Empty;

    [DisplayName("Дата санобработки")]
    public string SanitizationDate { get; set; } = string.Empty;

    [DisplayName("Текущий статус")]
    public string Status { get; set; } = string.Empty;

    [DisplayName("Тип топлива")]
    public string FuelType { get; set; } = string.Empty;

    [DisplayName("Базовая норма расхода (л/100км)")]
    public double BaseFuelConsumption { get; set; }
}

public class DriverExportDto
{
    [DisplayName("ФИО Сотрудника")]
    public string FullName { get; set; } = string.Empty;

    [DisplayName("Контактный телефон")]
    public string Phone { get; set; } = string.Empty;

    [DisplayName("E-mail")]
    public string Email { get; set; } = string.Empty;

    [DisplayName("Серия и номер ВУ")]
    public string LicenseNumber { get; set; } = string.Empty;

    [DisplayName("Категории")]
    public string LicenseCategories { get; set; } = string.Empty;

    [DisplayName("ВУ действительно до")]
    public string LicenseExpirationDate { get; set; } = string.Empty;

    [DisplayName("Номер мед. справки")]
    public string MedicalCertificateNumber { get; set; } = string.Empty;

    [DisplayName("Мед. справка действительна до")]
    public string MedicalCertificateExpiration { get; set; } = string.Empty;

    [DisplayName("Дата найма")]
    public string EmploymentDate { get; set; } = string.Empty;

    [DisplayName("Текущий статус")]
    public string Status { get; set; } = string.Empty;
}
using System;
using System.Collections.Generic;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class Vehicle : ISoftDeletable
{
    public int VehicleID { get; set; }
    public string RegNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string VIN { get; set; } = string.Empty;
    public int Year { get; set; }
    public int CapacityKG { get; set; }
    public double CapacityM3 { get; set; }
    public int Mileage { get; set; }
    public bool IsFridge { get; set; }
    public DateTime SanitizationDate { get; set; }
    public VehicleStatus Status { get; set; }
    public FuelType FuelType { get; set; }
    public double FuelCapacity { get; set; }
    public double CurrentFuelLevel { get; set; }
    public double BaseFuelConsumption { get; set; }
    public double CargoFuelBonus { get; set; }
    public double WinterFuelBonus { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual ICollection<VehicleServiceRecord> ServiceRecords { get; set; } = new List<VehicleServiceRecord>();
}
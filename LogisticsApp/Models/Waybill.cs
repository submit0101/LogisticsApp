using System;
using System.Collections.Generic;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class Waybill : ISoftDeletable
{
    public int WaybillID { get; set; }
    public DateTime DateCreate { get; set; }
    public DateTime? DateOut { get; set; }
    public DateTime? DateIn { get; set; }

    public int? OdometerOut { get; set; }
    public int? OdometerIn { get; set; }
    public double? FuelOut { get; set; }
    public double? FuelIn { get; set; }

    public double TotalDistance { get; set; }
    public double CalculatedFuelConsumption { get; set; }

    public string Notes { get; set; } = string.Empty;
    public bool IsPosted { get; set; }

    public WaybillStatus Status { get; set; }

    public int? VehicleID { get; set; }
    public virtual Vehicle? Vehicle { get; set; }

    public int? DriverID { get; set; }
    public virtual Driver? Driver { get; set; }

    public DateTime? DepartureTime { get; set; }
    public DateTime? ExpectedArrivalTime { get; set; }
    public DateTime? ActualArrivalTime { get; set; }

    public bool HasIncident { get; set; }
    public string? IncidentType { get; set; }
    public string? IncidentDescription { get; set; }
    public int DelayMinutes { get; set; }

    public virtual ICollection<WaybillPoint> Points { get; set; } = new List<WaybillPoint>();
    public virtual ICollection<FuelTicket> FuelTickets { get; set; } = new List<FuelTicket>();

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public void AssignTransportAndDriver(int vehicleId, int driverId)
    {
        VehicleID = vehicleId;
        DriverID = driverId;
    }
}
using System;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class VehicleServiceRecord
{
    public int RecordID { get; set; }
    public int VehicleID { get; set; }
    public virtual Vehicle? Vehicle { get; set; }
    public DateTime ServiceDate { get; set; }
    public VehicleServiceType ServiceType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int OdometerReading { get; set; }
    public string MechanicName { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
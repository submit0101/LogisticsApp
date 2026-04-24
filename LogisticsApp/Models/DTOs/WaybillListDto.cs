using System;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models.DTOs;

public class WaybillListDto
{
    public int WaybillID { get; set; }
    public DateTime DateCreate { get; set; }
    public string VehicleRegNumber { get; set; } = string.Empty;
    public string DriverFullName { get; set; } = string.Empty;
    public DateTime? DateOut { get; set; }
    public DateTime? DateIn { get; set; }
    public WaybillStatus Status { get; set; }
    public bool IsPosted { get; set; }
}
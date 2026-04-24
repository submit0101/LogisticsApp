using System;
using System.Collections.Generic;

namespace LogisticsApp.Models.DTOs.Reports;

public class RouteManifestDto
{
    public string WaybillNumber { get; set; } = string.Empty;
    public DateTime DispatchDate { get; set; }
    public string DriverFullName { get; set; } = string.Empty;
    public string VehicleRegistrationNumber { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public List<RouteManifestPointDto> Points { get; set; } = new();
}

public class RouteManifestPointDto
{
    public decimal BoxesCount { get; set; }
    public decimal CratesCount { get; set; }
    public int OrderNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal GrossWeight { get; set; }
}
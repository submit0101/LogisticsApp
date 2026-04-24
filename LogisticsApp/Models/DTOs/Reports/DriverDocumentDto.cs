using System;
using System.Collections.Generic;

namespace LogisticsApp.Models.DTOs.Reports;

public class DriverDocumentDto
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string DriverFullName { get; set; } = string.Empty;
    public string VehicleRegistrationNumber { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string ChiefAccountantName { get; set; } = string.Empty;
    public List<DriverDocumentItemDto> Items { get; set; } = new();
}

public class DriverDocumentItemDto
{
    public int SequenceNumber { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Weight { get; set; }
}
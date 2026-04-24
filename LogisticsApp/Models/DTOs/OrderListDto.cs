using System;
using LogisticsApp.Models;

namespace LogisticsApp.Models.DTOs;

public class OrderListDto
{
    public int OrderID { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Customer? Customer { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal TotalSum { get; set; }
    public double WeightKG { get; set; }
    public bool RequiredTempMode { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPosted { get; set; }
}
using System;
using System.Collections.Generic;

namespace LogisticsApp.Models.DTOs.Reports;

public class CustomerOrdersGroupDto
{
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalGroupSum { get; set; }
    public decimal TotalGroupWeight { get; set; }
    public List<OrderReportItemDto> Items { get; set; } = new();
}

public class OrderReportItemDto
{
    public int OrderID { get; set; }
    public DateTime OrderDate { get; set; }
    public string ProductSKU { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal TotalSum { get; set; }
    public decimal TotalWeight { get; set; }
}
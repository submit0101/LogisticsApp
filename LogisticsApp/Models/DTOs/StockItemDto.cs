namespace LogisticsApp.Models.DTOs;

public class StockItemDto
{
    public int ProductID { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int TotalQuantity => AvailableQuantity + ReservedQuantity;
}
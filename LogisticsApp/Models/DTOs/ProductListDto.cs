namespace LogisticsApp.Models.DTOs;

public class ProductListDto
{
    public int ProductID { get; set; }
    public int? GroupID { get; set; }
    public string GroupName { get; set; } = string.Empty; 
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public string ShelfLife { get; set; } = string.Empty; 
    public string StorageConditions { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
}
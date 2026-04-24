using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class InventoryTransaction
{
    [Key]
    public int TransactionID { get; set; }

    public DateTime Timestamp { get; set; }

    public int ProductID { get; set; }

    [ForeignKey("ProductID")]
    public virtual Product? Product { get; set; }

    public int WarehouseID { get; set; }

    [ForeignKey("WarehouseID")]
    public virtual Warehouse? Warehouse { get; set; }

    public int Quantity { get; set; }

    public bool IsReserve { get; set; }

    [MaxLength(50)]
    public string SourceDocument { get; set; } = string.Empty;

    public int SourceDocumentID { get; set; }

    public int? OrderID { get; set; }

    [ForeignKey("OrderID")]
    public virtual Order? Order { get; set; }

    public int? WaybillID { get; set; }

    [ForeignKey("WaybillID")]
    public virtual Waybill? Waybill { get; set; }
}
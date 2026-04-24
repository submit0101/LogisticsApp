using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class InventoryDocument : ISoftDeletable
{
    [Key]
    public int DocumentID { get; set; }
    public DateTime DocumentDate { get; set; } = DateTime.Now;
    public InventoryDocumentType Type { get; set; }
    public int WarehouseID { get; set; }
    [ForeignKey("WarehouseID")]
    public virtual Warehouse? Warehouse { get; set; }
    public int? OrderID { get; set; }
    [ForeignKey("OrderID")]
    public virtual Order? Order { get; set; }
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    public bool IsPosted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual ICollection<InventoryDocumentItem> Items { get; set; } = new List<InventoryDocumentItem>();
}
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class OrderItem : ISoftDeletable
{
    public int OrderItemID { get; set; }
    public int OrderID { get; set; }
    public virtual Order? Order { get; set; }
    public int ProductID { get; set; }
    public virtual Product? Product { get; set; }
    public int? PackagingID { get; set; }

    [ForeignKey("PackagingID")]
    public virtual ProductPackaging? Packaging { get; set; }

    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal TotalPrice { get; set; }
    public double TotalWeight { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
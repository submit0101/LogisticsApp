using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class Order : ISoftDeletable
{
    public int OrderID { get; set; }
    public int CustomerID { get; set; }
    public virtual Customer? Customer { get; set; }
    public int? WarehouseID { get; set; }

    [ForeignKey("WarehouseID")]
    public virtual Warehouse? Warehouse { get; set; }

    public DateTime OrderDate { get; set; }
    public double WeightKG { get; set; }
    public bool RequiredTempMode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPosted { get; set; }
    public decimal TotalSum { get; set; }
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;
    public OrderFulfillmentStatus FulfillmentStatus { get; set; } = OrderFulfillmentStatus.NotAllocated;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
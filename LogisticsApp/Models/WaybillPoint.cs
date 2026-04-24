using System;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class WaybillPoint : ISoftDeletable
{
    public int WP_ID { get; set; }
    public int WaybillID { get; set; }
    public virtual Waybill? Waybill { get; set; }
    public int OrderID { get; set; }
    public virtual Order? Order { get; set; }
    public int SequenceNumber { get; set; }
    public WaybillPointStatus Status { get; set; }

    public double? DeliveredWeightKG { get; set; }

    public DateTime? ActualDeliveryTime { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
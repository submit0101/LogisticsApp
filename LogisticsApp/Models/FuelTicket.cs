using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class FuelTicket
{
    [Key]
    public int TicketID { get; set; }

    public int WaybillID { get; set; }

    [ForeignKey("WaybillID")]
    public virtual Waybill? Waybill { get; set; }

    public DateTime TicketDate { get; set; } = DateTime.Now;

    public FuelType FuelType { get; set; }

    public double VolumeLiters { get; set; }

    public decimal PricePerLiter { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string TicketNumber { get; set; } = string.Empty;
}
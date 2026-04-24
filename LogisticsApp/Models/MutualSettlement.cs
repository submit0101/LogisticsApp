using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class MutualSettlement
{
    [Key]
    public int SettlementID { get; set; }

    public int CustomerID { get; set; }

    [ForeignKey("CustomerID")]
    public virtual Customer? Customer { get; set; }

    public int? OrderID { get; set; }

    [ForeignKey("OrderID")]
    public virtual Order? Order { get; set; }

    public DateTime Date { get; set; }

    public decimal Amount { get; set; }

    public MutualSettlementType Type { get; set; }

    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;
}
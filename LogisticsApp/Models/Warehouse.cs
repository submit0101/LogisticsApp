using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LogisticsApp.Models;

public class Warehouse : ISoftDeletable
{
    [Key]
    public int WarehouseID { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<InventoryDocument> Documents { get; set; } = new List<InventoryDocument>();

    public virtual ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}
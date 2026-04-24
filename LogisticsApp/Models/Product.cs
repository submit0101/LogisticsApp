using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class Product : ISoftDeletable
{
    public int ProductID { get; set; }
    public int? GroupID { get; set; }
    public virtual ProductGroup? Group { get; set; }
    public int? BaseUnitID { get; set; }

    [ForeignKey("BaseUnitID")]
    public virtual Unit? BaseUnit { get; set; }

    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShelfLife { get; set; } = string.Empty;
    public string StorageConditions { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
    public virtual ICollection<ProductPackaging> Packagings { get; set; } = new List<ProductPackaging>();
}
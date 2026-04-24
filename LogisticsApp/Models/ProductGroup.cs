using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class ProductGroup : ISoftDeletable
{
    [Key]
    public int GroupID { get; set; }
    public int? ParentGroupID { get; set; }

    [ForeignKey("ParentGroupID")]
    public virtual ProductGroup? ParentGroup { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual ICollection<ProductGroup> SubGroups { get; set; } = new List<ProductGroup>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
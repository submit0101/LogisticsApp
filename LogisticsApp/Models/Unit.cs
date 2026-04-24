using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LogisticsApp.Models;

public class Unit : ISoftDeletable
{
    [Key]
    public int UnitID { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<ProductPackaging> Packagings { get; set; } = new List<ProductPackaging>();
}
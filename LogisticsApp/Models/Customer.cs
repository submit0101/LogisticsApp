using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LogisticsApp.Models;

public enum CustomerType
{
    LegalEntity,
    Entrepreneur,
    PhysicalPerson
}

public class Customer : ISoftDeletable
{
    [Key]
    public int CustomerID { get; set; }
    [Required]
    public CustomerType Type { get; set; } = CustomerType.LegalEntity;
    [Required]
    [MaxLength(12)]
    public string INN { get; set; } = string.Empty;
    [MaxLength(9)]
    public string? KPP { get; set; }
    [MaxLength(15)]
    public string? OGRN { get; set; }
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? FullName { get; set; }
    [MaxLength(500)]
    public string? LegalAddress { get; set; }
    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? ContactPerson { get; set; }
    [MaxLength(50)]
    public string? Phone { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(9)]
    public string? BIK { get; set; }
    [MaxLength(200)]
    public string? BankName { get; set; }
    [MaxLength(20)]
    public string? CheckingAccount { get; set; }
    [MaxLength(20)]
    public string? CorrAccount { get; set; }
    public double? GeoLat { get; set; }
    public double? GeoLon { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
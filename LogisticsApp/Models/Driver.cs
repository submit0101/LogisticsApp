using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Models;

public class Driver : ISoftDeletable
{
    public int DriverID { get; set; }
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? MiddleName { get; set; }
    [MaxLength(500)]
    public string? PhotoPath { get; set; }
    [Required]
    [MaxLength(20)]
    public string LicenseNumber { get; set; } = string.Empty;
    [Required]
    [MaxLength(50)]
    public string LicenseCategories { get; set; } = string.Empty;
    public DateTime LicenseExpirationDate { get; set; }
    [MaxLength(200)]
    public string? PassportData { get; set; }
    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(150)]
    public string? Email { get; set; }
    public DateTime EmploymentDate { get; set; }
    public DateTime? DismissalDate { get; set; }
    public DriverStatus Status { get; set; }
    [MaxLength(50)]
    public string? MedicalCertificateNumber { get; set; }
    public DateTime? MedicalCertificateExpiration { get; set; }
    [MaxLength(150)]
    public string? EmergencyContact { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public virtual ICollection<Waybill> Waybills { get; set; } = new List<Waybill>();
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{LastName} {FirstName}"
        : $"{LastName} {FirstName} {MiddleName}";
}
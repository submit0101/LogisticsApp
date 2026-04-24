using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class AuditLog
{
    [Key]
    public int LogID { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int? UserID { get; set; }
    [ForeignKey("UserID")]
    public virtual User? User { get; set; }
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    [Required]
    [MaxLength(50)]
    public string EntityName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string RecordID { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
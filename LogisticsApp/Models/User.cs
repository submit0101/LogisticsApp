using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class User
{
    [Key]
    public int UserID { get; set; }

    [Required]
    [MaxLength(50)]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public int RoleID { get; set; }

    [ForeignKey("RoleID")]
    public virtual Role? Role { get; set; }

    [MaxLength(100)]
    public string? FullName { get; set; }
}
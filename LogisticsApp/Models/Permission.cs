using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LogisticsApp.Models;

public class Permission
{
    [Key]
    public int PermissionID { get; set; }

    [Required]
    [MaxLength(100)]
    public string SystemName { get; set; } = string.Empty; 

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty; 

    [Required]
    [MaxLength(100)]
    public string Module { get; set; } = string.Empty; 

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
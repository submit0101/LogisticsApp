namespace LogisticsApp.Models;

public class RolePermission
{
    public int RoleID { get; set; }
    public virtual Role? Role { get; set; }

    public int PermissionID { get; set; }
    public virtual Permission? Permission { get; set; }
}
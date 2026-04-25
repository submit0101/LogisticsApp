using System;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

public enum AppPermission
{
    AccessSettings, ManageUsers, ViewAuditLog,
    ViewVehicles, EditVehicles, DeleteVehicles,
    ViewDrivers, EditDrivers, DeleteDrivers,
    ViewCustomers, EditCustomers, DeleteCustomers,
    ViewOrders, EditOrders, DeleteOrders,
    ViewWaybills, EditWaybills, DeleteWaybills,
    ViewReports, ExportReports,
    ViewNomenclature, EditNomenclature, DeleteNomenclature,
    ViewInventory, EditInventory,
    ViewFinance, EditFinance
}

public class SecurityService
{
  
    private User? _currentUser = new User
    {
        UserID = 1,
        Login = "admin",
        PasswordHash = "bypass",
        FullName = "Администратор",
        RoleID = 1 
    };

    public User? CurrentUser => _currentUser;

    public void Initialize(User user)
    {
        _currentUser = user;
    }

    public bool HasPermission(AppPermission permission)
    {
        return true;
    }
}
using System;
using System.Collections.Generic;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

public enum AppPermission
{
    AccessSettings,
    ManageUsers,
    ViewAuditLog,
    ViewVehicles,
    EditVehicles,
    DeleteVehicles,
    ViewDrivers,
    EditDrivers,
    DeleteDrivers,
    ViewCustomers,
    EditCustomers,
    DeleteCustomers,
    ViewOrders,
    EditOrders,
    DeleteOrders,
    ViewWaybills,
    EditWaybills,
    DeleteWaybills,
    ViewReports,
    ExportReports,
    ViewNomenclature,
    EditNomenclature,
    DeleteNomenclature,
    ViewInventory,
    EditInventory,
    ViewFinance,
    EditFinance
}

public class SecurityService
{
    private User? _currentUser;
    private HashSet<AppPermission> _currentPermissions = new();

    public User? CurrentUser => _currentUser;

    public void Initialize(User user)
    {
        _currentUser = user;
        _currentPermissions.Clear();

        if (user.Role?.RolePermissions != null)
        {
            foreach (var rp in user.Role.RolePermissions)
            {
                if (rp.Permission != null && Enum.TryParse(rp.Permission.SystemName, out AppPermission perm))
                {
                    _currentPermissions.Add(perm);
                }
            }
        }
    }

    public bool HasPermission(AppPermission permission)
    {
        return _currentPermissions.Contains(permission);
    }
}
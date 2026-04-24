using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Models;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _sp;
    private readonly SecurityService _security;
    private readonly IHelpService _helpService;

    [ObservableProperty]
    private object _currentView;

    public string UserName { get; }
    public string UserRole { get; }
    public bool IsAdmin { get; }

    public bool CanViewReports => _security.HasPermission(AppPermission.ViewReports);
    public bool CanAccessSettings => _security.HasPermission(AppPermission.AccessSettings);
    public bool CanViewOrdersOrWaybills => _security.HasPermission(AppPermission.ViewOrders) || _security.HasPermission(AppPermission.ViewWaybills);
    public bool CanViewDictionaries => _security.HasPermission(AppPermission.ViewCustomers) || _security.HasPermission(AppPermission.ViewVehicles) || _security.HasPermission(AppPermission.ViewDrivers) || _security.HasPermission(AppPermission.ViewNomenclature);
    public bool CanViewInventory => _security.HasPermission(AppPermission.ViewInventory);
    public bool CanViewFinance => _security.HasPermission(AppPermission.ViewFinance);
    public bool CanViewNomenclature => _security.HasPermission(AppPermission.ViewNomenclature);

    public MainViewModel(User user, IServiceProvider sp)
    {
        _sp = sp;
        _security = _sp.GetRequiredService<SecurityService>();
        _helpService = _sp.GetRequiredService<IHelpService>();

        UserName = user.FullName ?? user.Login;
        UserRole = user.Role?.Name ?? string.Empty;
        IsAdmin = user.Role?.Name == "Администратор" || user.Role?.IsSystem == true;

        _currentView = _sp.GetRequiredService<HomeViewModel>();
    }

    [RelayCommand] private void NavigateHome() => CurrentView = _sp.GetRequiredService<HomeViewModel>();
    [RelayCommand] private void NavigateOrders() => CurrentView = _sp.GetRequiredService<OrdersViewModel>();
    [RelayCommand] private void NavigateWaybills() => CurrentView = _sp.GetRequiredService<WaybillsViewModel>();
    [RelayCommand(CanExecute = nameof(CanViewInventory))] private void NavigateInventory() => CurrentView = _sp.GetRequiredService<InventoryViewModel>();
    [RelayCommand(CanExecute = nameof(CanViewFinance))] private void NavigateFinance() => CurrentView = _sp.GetRequiredService<FinanceViewModel>();
    [RelayCommand(CanExecute = nameof(CanViewNomenclature))] private void NavigateNomenclature() => CurrentView = _sp.GetRequiredService<NomenclatureViewModel>();
    [RelayCommand] private void NavigateCustomers() => CurrentView = _sp.GetRequiredService<CustomersViewModel>();
    [RelayCommand] private void NavigateVehicles() => CurrentView = _sp.GetRequiredService<VehiclesViewModel>();
    [RelayCommand] private void NavigateDrivers() => CurrentView = _sp.GetRequiredService<DriversViewModel>();
    [RelayCommand(CanExecute = nameof(CanViewReports))] private void NavigateReports() => CurrentView = _sp.GetRequiredService<ReportsViewModel>();
    [RelayCommand(CanExecute = nameof(IsAdmin))] private void NavigateUsers() => CurrentView = _sp.GetRequiredService<UsersViewModel>();
    [RelayCommand(CanExecute = nameof(IsAdmin))] private void NavigateRoles() => CurrentView = _sp.GetRequiredService<RolesViewModel>();
    [RelayCommand(CanExecute = nameof(IsAdmin))] private void NavigateAudit() => CurrentView = _sp.GetRequiredService<AuditViewModel>();
    [RelayCommand(CanExecute = nameof(CanAccessSettings))] private void NavigateArchive() => CurrentView = _sp.GetRequiredService<ArchiveViewModel>();
    [RelayCommand(CanExecute = nameof(CanAccessSettings))] private void NavigateSettings() => CurrentView = _sp.GetRequiredService<SettingsViewModel>();
    [RelayCommand(CanExecute = nameof(IsAdmin))] private void NavigateLogs() => CurrentView = _sp.GetRequiredService<LogViewerViewModel>();

    [RelayCommand]
    private void ShowHelp()
    {
        if (CurrentView != null)
        {
            _helpService.ShowHelpForModule(CurrentView.GetType().Name);
        }
    }
}
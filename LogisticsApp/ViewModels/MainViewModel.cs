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

    // Свойства UserRole и IsAdmin удалены, так как ролей больше нет

    // Все свойства CanView... удалены. 
    // Теперь в XAML разметке нужно либо удалить привязки к ним, 
    // либо оставить их возвращающими всегда true (для совместимости).
    public bool CanViewReports => true;
    public bool CanAccessSettings => true;
    public bool CanViewOrdersOrWaybills => true;
    public bool CanViewDictionaries => true;
    public bool CanViewInventory => true;
    public bool CanViewFinance => true;
    public bool CanViewNomenclature => true;
    public bool IsAdmin => true;

    public MainViewModel(User user, IServiceProvider sp)
    {
        _sp = sp;
        _security = _sp.GetRequiredService<SecurityService>();
        _helpService = _sp.GetRequiredService<IHelpService>();

        UserName = user.FullName ?? user.Login;

        // Инициализация UserRole и IsAdmin удалена
        _currentView = _sp.GetRequiredService<HomeViewModel>();
    }

    [RelayCommand] private void NavigateHome() => CurrentView = _sp.GetRequiredService<HomeViewModel>();
    [RelayCommand] private void NavigateOrders() => CurrentView = _sp.GetRequiredService<OrdersViewModel>();
    [RelayCommand] private void NavigateWaybills() => CurrentView = _sp.GetRequiredService<WaybillsViewModel>();

    // У всех команд ниже удален параметр CanExecute
    [RelayCommand] private void NavigateInventory() => CurrentView = _sp.GetRequiredService<InventoryViewModel>();
    [RelayCommand] private void NavigateFinance() => CurrentView = _sp.GetRequiredService<FinanceViewModel>();
    [RelayCommand] private void NavigateNomenclature() => CurrentView = _sp.GetRequiredService<NomenclatureViewModel>();
    [RelayCommand] private void NavigateCustomers() => CurrentView = _sp.GetRequiredService<CustomersViewModel>();
    [RelayCommand] private void NavigateVehicles() => CurrentView = _sp.GetRequiredService<VehiclesViewModel>();
    [RelayCommand] private void NavigateDrivers() => CurrentView = _sp.GetRequiredService<DriversViewModel>();
    [RelayCommand] private void NavigateReports() => CurrentView = _sp.GetRequiredService<ReportsViewModel>();
    [RelayCommand] private void NavigateUsers() => CurrentView = _sp.GetRequiredService<UsersViewModel>();

    // Команда NavigateRoles удалена полностью

    [RelayCommand] private void NavigateAudit() => CurrentView = _sp.GetRequiredService<AuditViewModel>();
    [RelayCommand] private void NavigateArchive() => CurrentView = _sp.GetRequiredService<ArchiveViewModel>();
    [RelayCommand] private void NavigateSettings() => CurrentView = _sp.GetRequiredService<SettingsViewModel>();
    [RelayCommand] private void NavigateLogs() => CurrentView = _sp.GetRequiredService<LogViewerViewModel>();

    [RelayCommand]
    private void ShowHelp()
    {
        if (CurrentView != null)
        {
            _helpService.ShowHelpForModule(CurrentView.GetType().Name);
        }
    }
}
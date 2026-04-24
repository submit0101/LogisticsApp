using System;
using System.Windows;
using LogisticsApp.Models;
using LogisticsApp.ViewModels.Windows;
using LogisticsApp.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Services;

public class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public DialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool ShowConfirmation(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowWarning(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowMessageBox(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool ShowCustomerEditor(Customer? customer)
    {
        var vm = _serviceProvider.GetRequiredService<CustomerEditorViewModel>();
        vm.Initialize(customer);
        var window = _serviceProvider.GetRequiredService<CustomerEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowDriverEditor(Driver? driver)
    {
        var vm = _serviceProvider.GetRequiredService<DriverEditorViewModel>();
        vm.Initialize(driver);
        var window = _serviceProvider.GetRequiredService<DriverEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowVehicleEditor(Vehicle? vehicle)
    {
        var vm = _serviceProvider.GetRequiredService<VehicleEditorViewModel>();
        vm.Initialize(vehicle);
        var window = _serviceProvider.GetRequiredService<VehicleEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowOrderEditor(Order? order)
    {
        var vm = _serviceProvider.GetRequiredService<OrderEditorViewModel>();
        vm.Initialize(order);
        var window = _serviceProvider.GetRequiredService<OrderEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowWaybillEditor(Waybill? waybill)
    {
        var vm = _serviceProvider.GetRequiredService<WaybillEditorViewModel>();
        vm.Initialize(waybill);
        var window = _serviceProvider.GetRequiredService<WaybillEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowUserEditor(out string? newPasswordHash, User? user = null)
    {
        var vm = _serviceProvider.GetRequiredService<UserEditorViewModel>();
        vm.Initialize(user);
        var window = _serviceProvider.GetRequiredService<UserEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();

        newPasswordHash = null;
        if (isSaved)
        {
            var updatedUser = vm.GetUser();
            newPasswordHash = string.IsNullOrWhiteSpace(vm.PlainPassword) ? null : updatedUser.PasswordHash;
            if (user != null)
            {
                user.Login = updatedUser.Login;
                user.FullName = updatedUser.FullName;
                user.RoleID = updatedUser.RoleID;
            }
        }
        return isSaved;
    }

    public bool ShowVehicleServiceRecordEditor(out VehicleServiceRecord? updatedRecord, VehicleServiceRecord? record = null, int currentOdometer = 0)
    {
        var vm = _serviceProvider.GetRequiredService<VehicleServiceRecordEditorViewModel>();
        vm.Initialize(record, currentOdometer);
        var window = _serviceProvider.GetRequiredService<VehicleServiceRecordEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();

        updatedRecord = isSaved ? vm.GetRecord() : null;
        return isSaved;
    }

    public bool ShowProductGroupEditor(out ProductGroup? group, ProductGroup? existingGroup = null)
    {
        var vm = _serviceProvider.GetRequiredService<ProductGroupEditorViewModel>();
        vm.Initialize(existingGroup);
        var window = _serviceProvider.GetRequiredService<ProductGroupEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();

        group = isSaved ? vm.GetGroup() : null;
        return isSaved;
    }

    public bool ShowUnitEditor(out Unit? unit, Unit? existingUnit = null)
    {
        var vm = _serviceProvider.GetRequiredService<UnitEditorViewModel>();
        vm.Initialize(existingUnit);
        var window = _serviceProvider.GetRequiredService<UnitEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();

        unit = isSaved ? vm.GetUnit() : null;
        return isSaved;
    }

    public bool ShowProductEditor(int? productId = null)
    {
        var vm = _serviceProvider.GetRequiredService<ProductEditorViewModel>();
        vm.Initialize(productId);
        var window = _serviceProvider.GetRequiredService<ProductEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowNomenclaturePicker(out int? selectedProductId)
    {
        var vm = _serviceProvider.GetRequiredService<NomenclaturePickerViewModel>();
        var window = _serviceProvider.GetRequiredService<NomenclaturePickerWindow>();
        window.DataContext = vm;
        bool isSelected = false;
        vm.RequestClose += (selected) => { isSelected = selected; window.Close(); };
        window.ShowDialog();

        selectedProductId = isSelected ? vm.SelectedProductId : null;
        return isSelected;
    }

    public bool ShowInventoryDocumentEditor(InventoryDocument? document)
    {
        var vm = _serviceProvider.GetRequiredService<InventoryDocumentEditorViewModel>();
        vm.Initialize(document);
        var window = _serviceProvider.GetRequiredService<InventoryDocumentEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }

    public bool ShowPaymentDocumentEditor(MutualSettlement? settlement)
    {
        var vm = _serviceProvider.GetRequiredService<PaymentDocumentEditorViewModel>();
        vm.Initialize();
        var window = _serviceProvider.GetRequiredService<PaymentDocumentEditWindow>();
        window.DataContext = vm;
        bool isSaved = false;
        vm.RequestClose += (saved) => { isSaved = saved; window.Close(); };
        window.ShowDialog();
        return isSaved;
    }
}
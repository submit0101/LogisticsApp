using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Models;
using System.Collections.ObjectModel;

namespace LogisticsApp.ViewModels.Windows;

public partial class InventoryDocumentItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _productId;

    [ObservableProperty]
    private Product _product = new();

    [ObservableProperty]
    private int _quantity = 1;

    public ObservableCollection<ProductPackaging> AvailablePackagings { get; } = new();

    [ObservableProperty]
    private ProductPackaging? _selectedPackaging;

    public decimal TotalBaseQuantity => Quantity * (_selectedPackaging?.Coefficient ?? 1m);

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(TotalBaseQuantity));
    }

    partial void OnSelectedPackagingChanged(ProductPackaging? value)
    {
        OnPropertyChanged(nameof(TotalBaseQuantity));
    }

    [RelayCommand]
    private void IncreaseQuantity()
    {
        Quantity++;
    }

    [RelayCommand]
    private void DecreaseQuantity()
    {
        if (Quantity > 1)
            Quantity--;
    }
}
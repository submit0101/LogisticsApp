using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Models;
using LogisticsApp.ViewModels.Windows;

namespace LogisticsApp.ViewModels;

public partial class OrderItemViewModel : ObservableObject
{
    [ObservableProperty] private int _productId;
    [ObservableProperty] private Product _product = new();
    [ObservableProperty] private ObservableCollection<ProductPackaging> _availablePackagings = new();
    [ObservableProperty] private ProductPackaging? _selectedPackaging;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _totalPrice;
    [ObservableProperty] private double _totalWeight;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeficit))]
    private int _availableStock;

    public OrderEditorViewModel ParentViewModel { get; set; } = null!;

    public bool IsDeficit => Quantity > AvailableStock;

    [RelayCommand]
    private void IncreaseQuantity()
    {
        Quantity++;
    }

    [RelayCommand]
    private void DecreaseQuantity()
    {
        if (Quantity > 1)
        {
            Quantity--;
        }
    }

    partial void OnQuantityChanged(int value)
    {
        Calculate();
        ParentViewModel?.CalculateTotals();
        OnPropertyChanged(nameof(IsDeficit));
    }

    partial void OnPriceChanged(decimal value)
    {
        Calculate();
        ParentViewModel?.CalculateTotals();
    }

    partial void OnSelectedPackagingChanged(ProductPackaging? value)
    {
        Calculate();
        ParentViewModel?.CalculateTotals();
    }

    private void Calculate()
    {
        TotalPrice = Quantity * Price;
        if (SelectedPackaging != null)
        {
            TotalWeight = Quantity * SelectedPackaging.WeightKG;
        }
        else
        {
            TotalWeight = 0;
        }
    }
}
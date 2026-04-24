using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels.Windows;

public class NomenclatureItemDto
{
    public int ProductID { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProductGroup? ProductGroup { get; set; }
    public Unit? BaseUnit { get; set; }
    public decimal BasePrice { get; set; }
    public double AvailableStock { get; set; }
}

public partial class NomenclaturePickerViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly object _lockObj = new();

    // Кэш всех товаров для быстрой клиентской фильтрации без запросов к БД
    private List<NomenclatureItemDto> _allItems = new();

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _onlyInStock;

    // Коллекции для ComboBox и DataGrid
    public ObservableCollection<ProductGroup> AvailableProductGroups { get; } = new();
    [ObservableProperty] private ProductGroup? _selectedProductGroup;

    public ObservableCollection<NomenclatureItemDto> FilteredProducts { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    private NomenclatureItemDto? _selectedProduct;

    public int? SelectedProductId => SelectedProduct?.ProductID;

    public NomenclaturePickerViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        BindingOperations.EnableCollectionSynchronization(FilteredProducts, _lockObj);

        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var groups = await context.ProductGroups.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableProductGroups.Clear();
                foreach (var g in groups) AvailableProductGroups.Add(g);
            });

            var dbProducts = await context.Products
                .Include(p => p.Group)
                .Include(p => p.BaseUnit)
                .Include(p => p.Prices)
                .AsNoTracking()
                .ToListAsync();

            var stocks = await context.InventoryTransactions
                .GroupBy(t => t.ProductID)
                .Select(g => new
                {
                    ProductID = g.Key,
                    Available = g.Where(t => !t.IsReserve).Sum(t => t.Quantity) - Math.Abs(g.Where(t => t.IsReserve).Sum(t => t.Quantity))
                })
                .ToDictionaryAsync(x => x.ProductID, x => x.Available);

            var today = DateTime.Now;

            _allItems = dbProducts.Select(p => new NomenclatureItemDto
            {
                ProductID = p.ProductID,
                SKU = p.SKU,
                Name = p.Name,
                ProductGroup = p.Group,
                BaseUnit = p.BaseUnit,
                BasePrice = p.Prices
                    .Where(price => price.Period <= today)
                    .OrderByDescending(price => price.Period)
                    .Select(price => price.Value)
                    .FirstOrDefault(),
                AvailableStock = stocks.TryGetValue(p.ProductID, out var s) ? s : 0
            }).OrderBy(p => p.Name).ToList();

            ApplyFilters();
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error($"Ошибка загрузки товаров: {ex.Message}"));
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedProductGroupChanged(ProductGroup? value) => ApplyFilters();
    partial void OnOnlyInStockChanged(bool value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(p => p.Name.ToLower().Contains(search) || p.SKU.ToLower().Contains(search));
        }

        if (SelectedProductGroup != null)
        {
            filtered = filtered.Where(p => p.ProductGroup?.GroupID == SelectedProductGroup.GroupID);
        }

        if (OnlyInStock)
        {
            filtered = filtered.Where(p => p.AvailableStock > 0);
        }

        lock (_lockObj)
        {
            FilteredProducts.Clear();
            foreach (var item in filtered)
            {
                FilteredProducts.Add(item);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Confirm() => RequestClose?.Invoke(true);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Select() => RequestClose?.Invoke(true);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    private bool HasSelection() => SelectedProduct != null;
}
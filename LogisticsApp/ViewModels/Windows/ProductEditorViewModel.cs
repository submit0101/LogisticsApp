using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels.Windows;

public partial class ProductEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IDialogService _dialogService;

    private Product _currentProduct = new();

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private ObservableCollection<ProductGroup> _availableGroups = new();
    [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Артикул обязателен")]
    private string _sku = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Наименование обязательно")]
    private string _name = string.Empty;

    [ObservableProperty] private ProductGroup? _selectedGroup;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Базовая единица измерения обязательна")]
    private Unit? _selectedBaseUnit;

    [ObservableProperty] private string _shelfLife = string.Empty;
    [ObservableProperty] private string _storageConditions = string.Empty;
    [ObservableProperty] private string _barcode = string.Empty;

    public ObservableCollection<ProductPrice> Prices { get; } = new();
    [ObservableProperty] private ProductPrice? _selectedPrice;
    [ObservableProperty] private DateTime _newPriceDate = DateTime.Today;
    [ObservableProperty] private decimal _newPriceValue;

    public ObservableCollection<ProductPackaging> Packagings { get; } = new();
    [ObservableProperty] private ProductPackaging? _selectedPackaging;
    [ObservableProperty] private Unit? _newPackagingUnit;
    [ObservableProperty] private decimal _newPackagingCoefficient = 1;
    [ObservableProperty] private double _newPackagingWeight;
    [ObservableProperty] private double _newPackagingVolume;
    [ObservableProperty] private string _newPackagingBarcode = string.Empty;

    public ProductEditorViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, IDialogService dialogService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _dialogService = dialogService;
    }

    public void Initialize(int? productId)
    {
        _ = LoadDataAsync(productId);
    }

    private async Task LoadDataAsync(int? productId)
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var groups = await context.ProductGroups.OrderBy(g => g.Name).ToListAsync();
            var units = await context.Units.OrderBy(u => u.Name).ToListAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableGroups.Clear();
                foreach (var g in groups) AvailableGroups.Add(g);

                AvailableUnits.Clear();
                foreach (var u in units) AvailableUnits.Add(u);
            });

            if (productId.HasValue)
            {
                var product = await context.Products
                    .Include(p => p.Prices)
                    .Include(p => p.Packagings).ThenInclude(pkg => pkg.Unit)
                    .FirstOrDefaultAsync(p => p.ProductID == productId.Value);

                if (product != null)
                {
                    _currentProduct = product;
                    Sku = product.SKU;
                    Name = product.Name;
                    ShelfLife = product.ShelfLife ?? string.Empty;
                    StorageConditions = product.StorageConditions ?? string.Empty;
                    Barcode = product.Barcode ?? string.Empty;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedGroup = AvailableGroups.FirstOrDefault(g => g.GroupID == product.GroupID);
                        SelectedBaseUnit = AvailableUnits.FirstOrDefault(u => u.UnitID == product.BaseUnitID);

                        Prices.Clear();
                        foreach (var price in product.Prices.OrderByDescending(p => p.Period))
                        {
                            Prices.Add(price);
                        }

                        Packagings.Clear();
                        foreach (var pkg in product.Packagings)
                        {
                            Packagings.Add(pkg);
                        }
                    });
                }
            }
            else
            {
                _currentProduct = new Product();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Prices.Clear();
                    Packagings.Clear();
                });
            }
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddGroup()
    {
        if (_dialogService.ShowProductGroupEditor(out var newGroup) && newGroup != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableGroups.Add(newGroup);
                var sorted = AvailableGroups.OrderBy(g => g.Name).ToList();
                AvailableGroups.Clear();
                foreach (var g in sorted) AvailableGroups.Add(g);
                SelectedGroup = AvailableGroups.FirstOrDefault(g => g.GroupID == newGroup.GroupID);
                _notify.Success($"Группа '{newGroup.Name}' создана и выбрана.");
            });
        }
    }

    [RelayCommand]
    private void AddUnit()
    {
        if (_dialogService.ShowUnitEditor(out var newUnit) && newUnit != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableUnits.Add(newUnit);
                var sorted = AvailableUnits.OrderBy(u => u.Name).ToList();
                AvailableUnits.Clear();
                foreach (var u in sorted) AvailableUnits.Add(u);
                SelectedBaseUnit = AvailableUnits.FirstOrDefault(u => u.UnitID == newUnit.UnitID);
                _notify.Success($"Единица '{newUnit.Name}' создана.");
            });
        }
    }

    [RelayCommand]
    private void AddPrice()
    {
        if (NewPriceValue <= 0)
        {
            _notify.Warning("Цена должна быть больше нуля");
            return;
        }

        var existingPrice = Prices.FirstOrDefault(p => p.Period.Date == NewPriceDate.Date);
        if (existingPrice != null)
        {
            existingPrice.Value = NewPriceValue;
        }
        else
        {
            Prices.Add(new ProductPrice { Period = NewPriceDate.Date, Value = NewPriceValue });
        }

        var sorted = Prices.OrderByDescending(p => p.Period).ToList();
        Prices.Clear();
        foreach (var p in sorted) Prices.Add(p);
        NewPriceValue = 0;
    }

    [RelayCommand]
    private void RemovePrice()
    {
        if (SelectedPrice != null)
        {
            Prices.Remove(SelectedPrice);
        }
    }

    [RelayCommand]
    private void AddPackaging()
    {
        if (NewPackagingUnit == null)
        {
            _notify.Warning("Выберите единицу измерения для упаковки");
            return;
        }
        if (NewPackagingCoefficient <= 0)
        {
            _notify.Warning("Коэффициент должен быть больше нуля");
            return;
        }

        var pkg = new ProductPackaging
        {
            UnitID = NewPackagingUnit.UnitID,
            Unit = NewPackagingUnit,
            Coefficient = NewPackagingCoefficient,
            WeightKG = NewPackagingWeight,
            VolumeM3 = NewPackagingVolume,
            Barcode = NewPackagingBarcode
        };

        Packagings.Add(pkg);

        NewPackagingUnit = null;
        NewPackagingCoefficient = 1;
        NewPackagingWeight = 0;
        NewPackagingVolume = 0;
        NewPackagingBarcode = string.Empty;
    }

    [RelayCommand]
    private void RemovePackaging()
    {
        if (SelectedPackaging != null)
        {
            Packagings.Remove(SelectedPackaging);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors || SelectedBaseUnit == null)
        {
            _notify.Warning("Заполните все обязательные поля, включая базовую единицу измерения.");
            return;
        }

        if (Prices.Count == 0)
        {
            _notify.Warning("Необходимо установить хотя бы одну цену для товара.");
            return;
        }

        IsLoading = true;

        _currentProduct.SKU = Sku;
        _currentProduct.Name = Name;
        _currentProduct.ShelfLife = ShelfLife;
        _currentProduct.StorageConditions = StorageConditions;
        _currentProduct.Barcode = Barcode;
        _currentProduct.GroupID = SelectedGroup?.GroupID;
        _currentProduct.BaseUnitID = SelectedBaseUnit.UnitID;

        var basePackaging = Packagings.FirstOrDefault(p => p.Coefficient == 1);
        if (basePackaging != null)
        {
            basePackaging.UnitID = SelectedBaseUnit.UnitID;
            basePackaging.Unit = SelectedBaseUnit;
        }
        else
        {
            Packagings.Add(new ProductPackaging
            {
                UnitID = SelectedBaseUnit.UnitID,
                Unit = SelectedBaseUnit,
                Coefficient = 1,
                WeightKG = 0,
                VolumeM3 = 0,
                Barcode = Barcode
            });
        }

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            if (_currentProduct.ProductID == 0)
            {
                _currentProduct.Prices = Prices.ToList();

                foreach (var pkg in Packagings)
                {
                    pkg.Unit = null; 
                }
                _currentProduct.Packagings = Packagings.ToList();

                context.Products.Add(_currentProduct);
            }
            else
            {
                var dbProduct = await context.Products
                    .Include(p => p.Prices)
                    .Include(p => p.Packagings)
                    .FirstOrDefaultAsync(p => p.ProductID == _currentProduct.ProductID);

                if (dbProduct != null)
                {
                    context.Entry(dbProduct).CurrentValues.SetValues(_currentProduct);

                    var pricesToRemove = dbProduct.Prices.Where(dp => !Prices.Any(p => p.PriceID == dp.PriceID && p.PriceID != 0)).ToList();
                    foreach (var ptr in pricesToRemove) context.ProductPrices.Remove(ptr);

                    foreach (var price in Prices)
                    {
                        if (price.PriceID == 0)
                        {
                            price.ProductID = dbProduct.ProductID;
                            context.ProductPrices.Add(price);
                        }
                        else
                        {
                            var existing = dbProduct.Prices.FirstOrDefault(p => p.PriceID == price.PriceID);
                            if (existing != null)
                            {
                                existing.Period = price.Period;
                                existing.Value = price.Value;
                                context.Entry(existing).State = EntityState.Modified;
                            }
                        }
                    }

                    var pkgsToRemove = dbProduct.Packagings.Where(dp => !Packagings.Any(p => p.PackagingID == dp.PackagingID && p.PackagingID != 0)).ToList();
                    foreach (var pkr in pkgsToRemove) context.ProductPackagings.Remove(pkr);

                    foreach (var pkg in Packagings)
                    {
                        if (pkg.PackagingID == 0)
                        {
                            pkg.ProductID = dbProduct.ProductID;
                            pkg.Unit = null;
                            context.ProductPackagings.Add(pkg);
                        }
                        else
                        {
                            var existing = dbProduct.Packagings.FirstOrDefault(p => p.PackagingID == pkg.PackagingID);
                            if (existing != null)
                            {
                                existing.UnitID = pkg.UnitID;
                                existing.Coefficient = pkg.Coefficient;
                                existing.WeightKG = pkg.WeightKG;
                                existing.VolumeM3 = pkg.VolumeM3;
                                existing.Barcode = pkg.Barcode;
                                context.Entry(existing).State = EntityState.Modified;
                            }
                        }
                    }
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success("Номенклатура успешно сохранена. ВГХ синхронизированы.");
                RequestClose?.Invoke(true);
            });
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
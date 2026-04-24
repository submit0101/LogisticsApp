using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LogisticsApp.ViewModels.Windows;

public partial class OrderEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IDialogService _dialogService;
    private readonly SecurityService _security;
    private readonly InventoryService _inventoryService;

    private Order _currentOrder = new();
    private bool _isNew;

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private ObservableCollection<Customer> _availableCustomers = new();
    [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Контрагент обязателен")]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Склад отгрузки обязателен")]
    private Warehouse? _selectedWarehouse;

    [ObservableProperty] private OrderPriority _selectedPriority = OrderPriority.Normal;
    public Array AvailablePriorities => Enum.GetValues(typeof(OrderPriority));

    [ObservableProperty] private DateTime _orderDate = DateTime.Now;
    [ObservableProperty] private double _weightKG;
    [ObservableProperty] private bool _requiredTempMode;
    [ObservableProperty] private string _status = "Draft";
    [ObservableProperty] private OrderFulfillmentStatus _fulfillmentStatus = OrderFulfillmentStatus.NotAllocated;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private decimal _totalSum;

    [ObservableProperty] private OrderItemViewModel? _selectedItem;

    [ObservableProperty] private bool _isPosted;
    [ObservableProperty] private bool _hasDeficit;

    public ObservableCollection<OrderItemViewModel> Items { get; } = new();

    public bool IsEditable => !IsLoading && !IsPosted;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEditable));
    partial void OnIsPostedChanged(bool value) => OnPropertyChanged(nameof(IsEditable));

    async partial void OnOrderDateChanged(DateTime value)
    {
        if (Items.Count > 0 && IsEditable) await RecalculateHistoricalPricesAsync();
    }

    async partial void OnSelectedWarehouseChanged(Warehouse? value)
    {
        if (Items.Count > 0 && IsEditable && value != null)
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var productIds = Items.Select(i => i.ProductId).Distinct().ToList();
            var stocks = await _inventoryService.GetAvailableStockAsync(context, productIds, value.WarehouseID);

            foreach (var item in Items)
                item.AvailableStock = stocks.TryGetValue(item.ProductId, out var s) ? (int)Math.Round(s) : 0;

            CalculateTotals();
        }
    }

    public OrderEditorViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        IDialogService dialogService,
        SecurityService security,
        InventoryService inventoryService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _dialogService = dialogService;
        _security = security;
        _inventoryService = inventoryService;
    }

    public void Initialize(Order? order)
    {
        _isNew = order == null;
        _ = LoadDataAsync(order);
    }

    private async Task LoadDataAsync(Order? order)
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var customers = await context.Customers.OrderBy(c => c.Name).ToListAsync();
            var warehouses = await context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableCustomers.Clear();
                foreach (var c in customers) AvailableCustomers.Add(c);

                AvailableWarehouses.Clear();
                foreach (var w in warehouses) AvailableWarehouses.Add(w);
            });

            if (order != null)
            {
                _currentOrder = order;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedCustomer = AvailableCustomers.FirstOrDefault(c => c.CustomerID == order.CustomerID);
                    SelectedWarehouse = AvailableWarehouses.FirstOrDefault(w => w.WarehouseID == order.WarehouseID);
                    _orderDate = order.OrderDate;
                    OnPropertyChanged(nameof(OrderDate));
                    WeightKG = order.WeightKG;
                    RequiredTempMode = order.RequiredTempMode;
                    Status = order.Status;
                    SelectedPriority = order.Priority;
                    FulfillmentStatus = order.FulfillmentStatus;
                    Description = order.Description ?? string.Empty;
                    IsPosted = order.IsPosted;
                });

                var items = await context.OrderItems
                    .Include(i => i.Product)
                    .Include(i => i.Packaging).ThenInclude(pkg => pkg.Unit)
                    .Where(i => i.OrderID == order.OrderID)
                    .ToListAsync();

                var productIds = items.Select(i => i.ProductID).Distinct().ToList();
                int currentWarehouseId = order.WarehouseID ?? (warehouses.FirstOrDefault()?.WarehouseID ?? 0);
                var stocks = await _inventoryService.GetAvailableStockAsync(context, productIds, currentWarehouseId);

                System.Windows.Application.Current.Dispatcher.Invoke(() => Items.Clear());

                foreach (var item in items)
                {
                    var packagings = await context.ProductPackagings.Include(p => p.Unit).Where(p => p.ProductID == item.ProductID).ToListAsync();

                    var ovm = new OrderItemViewModel
                    {
                        ProductId = item.ProductID,
                        Product = item.Product ?? new Product(),
                        Quantity = item.Quantity,
                        Price = item.Price,
                        TotalPrice = item.TotalPrice,
                        TotalWeight = item.TotalWeight,
                        AvailableStock = stocks.TryGetValue(item.ProductID, out var stock) ? (int)Math.Round(stock) : 0,
                        ParentViewModel = this
                    };

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var p in packagings) ovm.AvailablePackagings.Add(p);
                        ovm.SelectedPackaging = ovm.AvailablePackagings.FirstOrDefault(p => p.PackagingID == item.PackagingID);
                        Items.Add(ovm);
                    });
                }
            }
            else
            {
                _currentOrder = new Order { OrderDate = DateTime.Now, Status = "Draft", IsPosted = false };
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedWarehouse = AvailableWarehouses.FirstOrDefault();
                    Status = "Draft";
                    SelectedPriority = OrderPriority.Normal;
                    FulfillmentStatus = OrderFulfillmentStatus.NotAllocated;
                    IsPosted = false;
                });
            }

            System.Windows.Application.Current.Dispatcher.Invoke(CalculateTotals);
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task AddProductAsync()
    {
        if (SelectedWarehouse == null)
        {
            _notify.Warning("Сначала выберите склад отгрузки.");
            return;
        }

        if (_dialogService.ShowNomenclaturePicker(out var productId) && productId.HasValue)
        {
            IsLoading = true;
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();
                var product = await context.Products.FindAsync(productId.Value);
                if (product == null) return;

                var activePrice = await context.ProductPrices
                    .Where(pp => pp.ProductID == product.ProductID && pp.Period.Date <= OrderDate.Date)
                    .OrderByDescending(pp => pp.Period)
                    .Select(pp => pp.Value)
                    .FirstOrDefaultAsync();

                var stocks = await _inventoryService.GetAvailableStockAsync(context, new[] { product.ProductID }, SelectedWarehouse.WarehouseID);
                int currentStock = stocks.TryGetValue(product.ProductID, out var s) ? (int)Math.Round(s) : 0;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var existingItem = Items.FirstOrDefault(i => i.ProductId == product.ProductID);
                    if (existingItem != null)
                    {
                        existingItem.Quantity++;
                        existingItem.Price = activePrice;
                        existingItem.AvailableStock = currentStock;
                    }
                    else
                    {
                        var packagings = context.ProductPackagings.Include(p => p.Unit).Where(p => p.ProductID == product.ProductID).ToList();

                        var ovm = new OrderItemViewModel
                        {
                            ProductId = product.ProductID,
                            Product = product,
                            Quantity = 1,
                            Price = activePrice,
                            AvailableStock = currentStock,
                            ParentViewModel = this
                        };

                        foreach (var p in packagings) ovm.AvailablePackagings.Add(p);
                        ovm.SelectedPackaging = ovm.AvailablePackagings.FirstOrDefault();

                        Items.Add(ovm);
                    }
                    CalculateTotals();
                });
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
            }
        }
    }

    [RelayCommand]
    private void RemoveProduct()
    {
        if (SelectedItem != null)
        {
            Items.Remove(SelectedItem);
            CalculateTotals();
        }
    }

    private async Task RecalculateHistoricalPricesAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            foreach (var item in Items)
            {
                var activePrice = await context.ProductPrices
                    .Where(pp => pp.ProductID == item.ProductId && pp.Period.Date <= OrderDate.Date)
                    .OrderByDescending(pp => pp.Period)
                    .Select(pp => pp.Value)
                    .FirstOrDefaultAsync();

                item.Price = activePrice;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CalculateTotals();
                _notify.Success($"Цены в заказе пересчитаны согласно прайс-листу на дату {OrderDate:dd.MM.yyyy}");
            });
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    public void CalculateTotals()
    {
        TotalSum = Items.Sum(i => i.TotalPrice);
        WeightKG = Items.Sum(i => i.TotalWeight);
        HasDeficit = Items.Any(i => i.IsDeficit);
    }

    [RelayCommand]
    private async Task CreateWarehouseRequestAsync()
    {
        if (_currentOrder.OrderID == 0)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Warning("Для создания заявки необходимо сначала сохранить заказ как черновик."));
            return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var deficitItems = Items.Where(i => i.IsDeficit).ToList();
            if (!deficitItems.Any()) return;

            var newDoc = new InventoryDocument
            {
                DocumentDate = DateTime.Now,
                Type = InventoryDocumentType.Request,
                WarehouseID = SelectedWarehouse!.WarehouseID,
                OrderID = _currentOrder.OrderID,
                Reason = $"Заявка на пополнение под заказ №{_currentOrder.OrderID}",
                IsPosted = false,
                Items = deficitItems.Select(i => new InventoryDocumentItem { ProductID = i.ProductId, Quantity = i.Quantity - i.AvailableStock, CostPrice = i.Price }).ToList()
            };

            context.InventoryDocuments.Add(newDoc);

            context.AuditLogs.Add(new AuditLog { Action = "Создание", EntityName = "Документы склада", Details = $"Сгенерирована заявка на склад под заказ №{_currentOrder.OrderID}", Timestamp = DateTime.Now, UserID = _security.CurrentUser?.UserID });

            await context.SaveChangesAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Success("Заявка успешно отправлена на склад!"));
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    [RelayCommand] private async Task SaveDraftAsync() => await SaveInternalAsync(false);

    [RelayCommand] private async Task PostAsync() => await SaveInternalAsync(true);

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (Status == "Delivered" || Status == "InTransit")
        {
            _notify.Error("Нельзя отменить заказ, который уже доставляется или доставлен. Отмените путевой лист.");
            return;
        }
        Status = "Cancelled";
        await SaveInternalAsync(true);
    }

    [RelayCommand]
    private async Task UnpostAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            var originalOrder = await context.Orders.FirstOrDefaultAsync(o => o.OrderID == _currentOrder.OrderID);

            if (originalOrder != null)
            {
                if (originalOrder.Status == "Planned" || originalOrder.Status == "InTransit" || originalOrder.Status == "Delivered")
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error("Заказ привязан к путевому листу! Сначала отмените проведение путевого листа."));
                    return;
                }

                bool hasDirectPayments = await context.MutualSettlements.AnyAsync(ms =>
                    ms.OrderID == originalOrder.OrderID &&
                    ms.Type == MutualSettlementType.DebtDecrease &&
                    !ms.Description.Contains("Зачтена из аванса"));

                if (hasDirectPayments)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error("КРИТИЧЕСКАЯ БЛОКИРОВКА: По данному заказу принята прямая оплата (ПКО). Невозможно отменить проведение. Сначала отмените платеж в модуле Казначейства."));
                    return;
                }

                var autoAllocatedAdvances = await context.MutualSettlements
                    .Where(ms => ms.OrderID == originalOrder.OrderID && ms.Description == "Оплата зачтена из аванса")
                    .ToListAsync();

                foreach (var alloc in autoAllocatedAdvances)
                {
                    var offset = await context.MutualSettlements.FirstOrDefaultAsync(ms =>
                        ms.OrderID == null &&
                        ms.Description == $"Зачет аванса в счет заказа №{originalOrder.OrderID}" &&
                        ms.Amount == alloc.Amount &&
                        ms.Date == alloc.Date);

                    if (offset != null) context.MutualSettlements.Remove(offset);
                    context.MutualSettlements.Remove(alloc);
                }

                await _inventoryService.ReleaseOrderAllocationAsync(context, originalOrder.OrderID);

                originalOrder.IsPosted = false;
                originalOrder.Status = "Draft";

                var existingDebtIncrease = await context.MutualSettlements.FirstOrDefaultAsync(ms => ms.OrderID == originalOrder.OrderID && ms.Type == MutualSettlementType.DebtIncrease);
                if (existingDebtIncrease != null) context.MutualSettlements.Remove(existingDebtIncrease);

                context.AuditLogs.Add(new AuditLog { Action = "Отмена проведения", EntityName = "Заказы покупателей", Details = $"Отмена проведения заказа {originalOrder.OrderID}, резервы и авансы откачены.", Timestamp = DateTime.Now, UserID = _security.CurrentUser?.UserID });

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _currentOrder.IsPosted = false;
                    IsPosted = false;
                    Status = "Draft";
                    _notify.Success("Проведение отменено, резервы сняты, зачтенные авансы возвращены на баланс клиента.");
                });
            }
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    private async Task SaveInternalAsync(bool postDocument)
    {
        CalculateTotals();
        ValidateAllProperties();

        if (HasErrors || SelectedCustomer == null || SelectedWarehouse == null) return;

        IsLoading = true;

        _currentOrder.CustomerID = SelectedCustomer.CustomerID;
        _currentOrder.WarehouseID = SelectedWarehouse.WarehouseID;
        _currentOrder.OrderDate = OrderDate;
        _currentOrder.WeightKG = WeightKG;
        _currentOrder.RequiredTempMode = RequiredTempMode;
        _currentOrder.Description = Description;
        _currentOrder.Priority = SelectedPriority;
        _currentOrder.TotalSum = TotalSum;

        if (!postDocument) Status = "Draft";
        else if (Status == "Draft" || Status == "Cancelled") Status = "New";

        _currentOrder.Status = Status;
        _currentOrder.IsPosted = postDocument;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            if (postDocument && Status == "New")
            {
                var itemsForCheck = Items.Select(i => new OrderItem { ProductID = i.ProductId, Quantity = i.Quantity }).ToList();
                await _inventoryService.EnsureStockSufficientAsync(context, itemsForCheck, SelectedWarehouse.WarehouseID);
            }

            if (_currentOrder.OrderID == 0)
            {
                _currentOrder.Items = Items.Select(i => new OrderItem { ProductID = i.ProductId, PackagingID = i.SelectedPackaging?.PackagingID, Quantity = i.Quantity, Price = i.Price, TotalPrice = i.TotalPrice, TotalWeight = i.TotalWeight }).ToList();
                context.Orders.Add(_currentOrder);
                await context.SaveChangesAsync();
            }
            else
            {
                var originalOrder = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderID == _currentOrder.OrderID);
                if (originalOrder != null)
                {
                    context.Entry(originalOrder).CurrentValues.SetValues(_currentOrder);

                    var itemsToRemove = originalOrder.Items.Where(oi => !Items.Any(ni => ni.ProductId == oi.ProductID)).ToList();
                    foreach (var item in itemsToRemove) context.OrderItems.Remove(item);

                    foreach (var ni in Items)
                    {
                        var ep = originalOrder.Items.FirstOrDefault(oi => oi.ProductID == ni.ProductId);
                        if (ep != null)
                        {
                            ep.PackagingID = ni.SelectedPackaging?.PackagingID;
                            ep.Quantity = ni.Quantity;
                            ep.Price = ni.Price;
                            ep.TotalPrice = ni.TotalPrice;
                            ep.TotalWeight = ni.TotalWeight;
                            context.Entry(ep).State = EntityState.Modified;
                        }
                        else
                        {
                            context.OrderItems.Add(new OrderItem { OrderID = originalOrder.OrderID, ProductID = ni.ProductId, PackagingID = ni.SelectedPackaging?.PackagingID, Quantity = ni.Quantity, Price = ni.Price, TotalPrice = ni.TotalPrice, TotalWeight = ni.TotalWeight });
                        }
                    }
                    await context.SaveChangesAsync();
                }
            }

            if (postDocument && Status == "New") await _inventoryService.AllocateOrderAsync(context, _currentOrder);
            else if (!postDocument || Status == "Cancelled") await _inventoryService.ReleaseOrderAllocationAsync(context, _currentOrder.OrderID);

            if (postDocument && Status == "Delivered")
            {
                var existingSettlement = await context.MutualSettlements.FirstOrDefaultAsync(ms => ms.OrderID == _currentOrder.OrderID && ms.Type == MutualSettlementType.DebtIncrease);

                if (existingSettlement == null)
                {
                    context.MutualSettlements.Add(new MutualSettlement { CustomerID = _currentOrder.CustomerID, OrderID = _currentOrder.OrderID, Date = _currentOrder.OrderDate, Amount = TotalSum, Type = MutualSettlementType.DebtIncrease, Description = $"Отгрузка по заказу №{_currentOrder.OrderID}" });

                    var paidForOrder = await context.MutualSettlements.Where(ms => ms.OrderID == _currentOrder.OrderID && ms.Type == MutualSettlementType.DebtDecrease).SumAsync(ms => ms.Amount);
                    decimal debtForOrder = TotalSum - paidForOrder;

                    if (debtForOrder > 0)
                    {
                        var totalAdvances = await context.MutualSettlements.Where(ms => ms.CustomerID == _currentOrder.CustomerID && ms.OrderID == null && ms.Type == MutualSettlementType.DebtDecrease).SumAsync(ms => ms.Amount);
                        var usedAdvances = await context.MutualSettlements.Where(ms => ms.CustomerID == _currentOrder.CustomerID && ms.OrderID == null && ms.Type == MutualSettlementType.DebtIncrease).SumAsync(ms => ms.Amount);

                        decimal availableAdvance = totalAdvances - usedAdvances;

                        if (availableAdvance > 0)
                        {
                            decimal amountToApply = Math.Min(debtForOrder, availableAdvance);

                            context.MutualSettlements.Add(new MutualSettlement
                            {
                                CustomerID = _currentOrder.CustomerID,
                                OrderID = null,
                                Date = DateTime.Now,
                                Amount = amountToApply,
                                Type = MutualSettlementType.DebtIncrease,
                                Description = $"Зачет аванса в счет заказа №{_currentOrder.OrderID}"
                            });

                            context.MutualSettlements.Add(new MutualSettlement
                            {
                                CustomerID = _currentOrder.CustomerID,
                                OrderID = _currentOrder.OrderID,
                                Date = DateTime.Now,
                                Amount = amountToApply,
                                Type = MutualSettlementType.DebtDecrease,
                                Description = "Оплата зачтена из аванса"
                            });
                        }
                    }
                }
                else
                {
                    existingSettlement.Amount = TotalSum;
                    existingSettlement.CustomerID = _currentOrder.CustomerID;
                    context.MutualSettlements.Update(existingSettlement);
                }
            }
            else if (!postDocument || Status == "Cancelled")
            {
                var existingSettlement = await context.MutualSettlements.FirstOrDefaultAsync(ms => ms.OrderID == _currentOrder.OrderID && ms.Type == MutualSettlementType.DebtIncrease);
                if (existingSettlement != null) context.MutualSettlements.Remove(existingSettlement);
            }

            context.AuditLogs.Add(new AuditLog { Action = postDocument ? "Проведение" : (_isNew ? "Создание" : "Изменение"), EntityName = "Заказы покупателей", Details = $"Заказ ID:{_currentOrder.OrderID} на {TotalSum} ₽. Проведен: {postDocument}, Приоритет: {SelectedPriority}", Timestamp = DateTime.Now, UserID = _security.CurrentUser?.UserID });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success(postDocument ? "Документ успешно проведен. Резервы WMS распределены." : "Документ сохранен в статусе 'В подготовке'");
                RequestClose?.Invoke(true);
            });
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
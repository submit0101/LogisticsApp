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

namespace LogisticsApp.ViewModels.Windows;

public partial class InventoryDocumentEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IDialogService _dialogService;
    private readonly SecurityService _security;

    private InventoryDocument _currentDocument = new();

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTime _documentDate = DateTime.Now;
    [ObservableProperty] private InventoryDocumentType _type = InventoryDocumentType.Receipt;

    public Array AvailableDocumentTypes => Enum.GetValues(typeof(InventoryDocumentType));

    [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Склад обязателен")]
    private Warehouse? _selectedWarehouse;

    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private bool _isPosted;
    [ObservableProperty] private int? _orderId;

    public ObservableCollection<InventoryDocumentItemViewModel> Items { get; } = new();

    [ObservableProperty] private InventoryDocumentItemViewModel? _selectedItem;

    public bool IsEditable => !IsLoading && !IsPosted;
    public bool IsRequest => Type == InventoryDocumentType.Request && !IsPosted;
    public bool IsStandardEditable => IsEditable && !IsRequest;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(IsStandardEditable));
    }

    partial void OnIsPostedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(IsRequest));
        OnPropertyChanged(nameof(IsStandardEditable));
    }

    partial void OnTypeChanged(InventoryDocumentType value)
    {
        OnPropertyChanged(nameof(IsRequest));
        OnPropertyChanged(nameof(IsStandardEditable));
    }

    public InventoryDocumentEditorViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, IDialogService dialogService, SecurityService security)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _dialogService = dialogService;
        _security = security;
    }

    public void Initialize(InventoryDocument? document)
    {
        _ = LoadDataAsync(document);
    }

    private async Task LoadDataAsync(InventoryDocument? document)
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var warehouses = await context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableWarehouses.Clear();
                foreach (var w in warehouses) AvailableWarehouses.Add(w);
            });

            if (document != null)
            {
                _currentDocument = document;
                DocumentDate = document.DocumentDate;
                Type = document.Type;
                Reason = document.Reason ?? string.Empty;
                IsPosted = document.IsPosted;
                OrderId = document.OrderID;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedWarehouse = AvailableWarehouses.FirstOrDefault(w => w.WarehouseID == document.WarehouseID);
                });

                // ИНТЕГРАЦИЯ ВГХ: Загружаем строки вместе с упаковками
                var items = await context.InventoryDocumentItems
                    .Include(i => i.Product)
                    .Include(i => i.Packaging).ThenInclude(p => p.Unit)
                    .Where(i => i.DocumentID == document.DocumentID)
                    .ToListAsync();

                System.Windows.Application.Current.Dispatcher.Invoke(() => Items.Clear());

                foreach (var item in items)
                {
                    // Подтягиваем все возможные упаковки для данного товара
                    var packagings = await context.ProductPackagings
                        .Include(p => p.Unit)
                        .Where(p => p.ProductID == item.ProductID)
                        .ToListAsync();

                    var vm = new InventoryDocumentItemViewModel
                    {
                        ProductId = item.ProductID,
                        Product = item.Product ?? new Product(),
                        Quantity = item.Quantity
                    };

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var p in packagings) vm.AvailablePackagings.Add(p);
                        vm.SelectedPackaging = vm.AvailablePackagings.FirstOrDefault(p => p.PackagingID == item.PackagingID);
                        Items.Add(vm);
                    });
                }
            }
            else
            {
                _currentDocument = new InventoryDocument { DocumentDate = DateTime.Now, IsPosted = false };
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedWarehouse = AvailableWarehouses.FirstOrDefault();
                    DocumentDate = DateTime.Now;
                    Type = InventoryDocumentType.Receipt;
                    IsPosted = false;
                    Items.Clear();
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

    [RelayCommand]
    private async Task AddItemAsync()
    {
        if (_dialogService.ShowNomenclaturePicker(out var productId) && productId.HasValue)
        {
            IsLoading = true;
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();
                var product = await context.Products.FindAsync(productId.Value);
                if (product == null) return;

                // Загружаем упаковки для выбранного товара
                var packagings = await context.ProductPackagings
                    .Include(p => p.Unit)
                    .Where(p => p.ProductID == product.ProductID)
                    .ToListAsync();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var existingItem = Items.FirstOrDefault(i => i.ProductId == product.ProductID);
                    if (existingItem != null)
                    {
                        existingItem.Quantity++;
                    }
                    else
                    {
                        var vm = new InventoryDocumentItemViewModel
                        {
                            ProductId = product.ProductID,
                            Product = product,
                            Quantity = 1
                        };

                        foreach (var p in packagings) vm.AvailablePackagings.Add(p);
                        // По умолчанию выбираем базовую упаковку (с коэффициентом 1)
                        vm.SelectedPackaging = vm.AvailablePackagings.FirstOrDefault(p => p.Coefficient == 1m) ?? vm.AvailablePackagings.FirstOrDefault();

                        Items.Add(vm);
                    }
                });
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (SelectedItem != null)
        {
            Items.Remove(SelectedItem);
        }
    }

    [RelayCommand]
    private async Task FulfillRequestAsync()
    {
        Type = InventoryDocumentType.Receipt;
        Reason = "Исполнение заявки: " + Reason;
        await SaveInternalAsync(true);
    }

    [RelayCommand] private async Task SaveDraftAsync() => await SaveInternalAsync(false);
    [RelayCommand] private async Task PostAsync() => await SaveInternalAsync(true);

    [RelayCommand]
    private async Task UnpostAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            var originalDoc = await context.InventoryDocuments.FirstOrDefaultAsync(d => d.DocumentID == _currentDocument.DocumentID);
            if (originalDoc != null && originalDoc.IsPosted)
            {
                var existingTx = await context.InventoryTransactions.Where(t => t.SourceDocument == "InventoryDocument" && t.SourceDocumentID == originalDoc.DocumentID).ToListAsync();
                if (existingTx.Any())
                {
                    context.InventoryTransactions.RemoveRange(existingTx);
                }

                originalDoc.IsPosted = false;

                var auditLog = new AuditLog
                {
                    Action = "Отмена проведения",
                    EntityName = "Документы склада",
                    Details = $"Отмена проведения документа №{originalDoc.DocumentID} ({originalDoc.Type})",
                    Timestamp = DateTime.Now,
                    UserID = _security.CurrentUser?.UserID
                };
                context.AuditLogs.Add(auditLog);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _currentDocument.IsPosted = false;
                    IsPosted = false;
                    _notify.Success("Проведение отменено. Движения по складу удалены.");
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
        ValidateAllProperties();
        if (HasErrors || SelectedWarehouse == null) return;

        if (Items.Count == 0)
        {
            _notify.Warning("Документ не может быть пустым.");
            return;
        }

        IsLoading = true;

        _currentDocument.WarehouseID = SelectedWarehouse.WarehouseID;
        _currentDocument.DocumentDate = DocumentDate;
        _currentDocument.Type = Type;
        _currentDocument.Reason = Reason;
        _currentDocument.IsPosted = postDocument;
        _currentDocument.OrderID = OrderId;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            if (postDocument && Type == InventoryDocumentType.WriteOff)
            {
                var productIds = Items.Select(i => i.ProductId).Distinct().ToList();
                var balances = await context.InventoryTransactions
                    .Where(t => productIds.Contains(t.ProductID) && t.WarehouseID == SelectedWarehouse.WarehouseID)
                    .GroupBy(t => t.ProductID)
                    .Select(g => new { ProductID = g.Key, Available = g.Sum(t => t.Quantity) })
                    .ToDictionaryAsync(x => x.ProductID, x => x.Available);

                foreach (var item in Items)
                {
                    int available = balances.TryGetValue(item.ProductId, out var b) ? b : 0;
                    // ИНТЕГРАЦИЯ ВГХ: Проверяем наличие на складе в БАЗОВЫХ единицах!
                    int requiredBaseQty = (int)Math.Round(item.TotalBaseQuantity);

                    if (available < requiredBaseQty)
                    {
                        throw new InvalidOperationException($"Недостаточно остатков для списания '{item.Product.Name}'. Доступно (в баз. ед.): {available}, Требуется: {requiredBaseQty}.");
                    }
                }
            }

            if (_currentDocument.DocumentID == 0)
            {
                // ИНТЕГРАЦИЯ ВГХ: Сохраняем PackagingID
                _currentDocument.Items = Items.Select(i => new InventoryDocumentItem
                {
                    ProductID = i.ProductId,
                    Quantity = i.Quantity,
                    PackagingID = i.SelectedPackaging?.PackagingID
                }).ToList();

                context.InventoryDocuments.Add(_currentDocument);
                await context.SaveChangesAsync();
            }
            else
            {
                var originalDoc = await context.InventoryDocuments.Include(d => d.Items).FirstOrDefaultAsync(d => d.DocumentID == _currentDocument.DocumentID);
                if (originalDoc != null)
                {
                    context.Entry(originalDoc).CurrentValues.SetValues(_currentDocument);

                    var itemsToRemove = originalDoc.Items.Where(oi => !Items.Any(ni => ni.ProductId == oi.ProductID)).ToList();
                    foreach (var item in itemsToRemove) context.InventoryDocumentItems.Remove(item);

                    foreach (var ni in Items)
                    {
                        var ep = originalDoc.Items.FirstOrDefault(oi => oi.ProductID == ni.ProductId);
                        if (ep != null)
                        {
                            ep.Quantity = ni.Quantity;
                            ep.PackagingID = ni.SelectedPackaging?.PackagingID; // Обновляем упаковку
                            context.Entry(ep).State = EntityState.Modified;
                        }
                        else
                        {
                            context.InventoryDocumentItems.Add(new InventoryDocumentItem
                            {
                                DocumentID = originalDoc.DocumentID,
                                ProductID = ni.ProductId,
                                Quantity = ni.Quantity,
                                PackagingID = ni.SelectedPackaging?.PackagingID
                            });
                        }
                    }
                    await context.SaveChangesAsync();
                }
            }

            // САМОЕ ГЛАВНОЕ: Формирование движений по складу (InventoryTransactions)
            if (postDocument && Type != InventoryDocumentType.Request)
            {
                var existingTx = await context.InventoryTransactions.Where(t => t.SourceDocument == "InventoryDocument" && t.SourceDocumentID == _currentDocument.DocumentID).ToListAsync();
                if (existingTx.Any()) context.InventoryTransactions.RemoveRange(existingTx);

                foreach (var item in Items)
                {
                    // ИНТЕГРАЦИЯ ВГХ: На склад пишутся ТОЛЬКО базовые единицы (Quantity * Coefficient)
                    int baseQty = (int)Math.Round(item.TotalBaseQuantity);
                    int finalQty = Type == InventoryDocumentType.Receipt ? baseQty : -baseQty;

                    context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        Timestamp = DateTime.Now,
                        ProductID = item.ProductId,
                        WarehouseID = _currentDocument.WarehouseID,
                        Quantity = finalQty, // <-- В базу пишется пересчитанное количество
                        IsReserve = false,
                        SourceDocument = "InventoryDocument",
                        SourceDocumentID = _currentDocument.DocumentID
                    });
                }
            }

            var actionStr = postDocument ? "Проведение" : (_currentDocument.DocumentID == 0 ? "Создание" : "Изменение");
            context.AuditLogs.Add(new AuditLog { Action = actionStr, EntityName = "Документы склада", Details = $"Документ №{_currentDocument.DocumentID}. Тип: {Type}. Проведен: {postDocument}", Timestamp = DateTime.Now, UserID = _security.CurrentUser?.UserID });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success(postDocument ? "Документ проведен. Движения в базовых ед. зарегистрированы." : "Документ сохранен.");
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
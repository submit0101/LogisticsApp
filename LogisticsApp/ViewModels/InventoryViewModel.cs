using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.DTOs;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly IDialogService _dialogService;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;
    private readonly IInventoryReportService _inventoryReportService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchQuery = string.Empty;

    // Периоды для Оборотно-сальдовой ведомости
    [ObservableProperty] private DateTime? _dateFromFilter = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _dateToFilter = DateTime.Today;

    public ObservableCollection<StockItemDto> Stocks { get; } = new();
    public ObservableCollection<InventoryDocument> Documents { get; } = new();

    [ObservableProperty] private InventoryDocument? _selectedDocument;

    public InventoryViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        IDialogService dialogService,
        NotificationService notify,
        SecurityService security,
        IInventoryReportService inventoryReportService)
    {
        _dbFactory = dbFactory;
        _dialogService = dialogService;
        _notify = notify;
        _security = security;
        _inventoryReportService = inventoryReportService;
    }

    public async Task InitializeAsync()
    {
        await LoadStocksAsync();
        await LoadDocumentsAsync();
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        await LoadStocksAsync();
        await LoadDocumentsAsync();
    }

    [RelayCommand]
    private async Task LoadStocksAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewInventory)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var txs = await context.InventoryTransactions
                .Include(t => t.Product)
                .Include(t => t.Warehouse)
                .AsNoTracking()
                .ToListAsync();

            var aggregated = txs
                .GroupBy(t => new { t.ProductID, t.WarehouseID })
                .Select(g => new StockItemDto
                {
                    ProductID = g.Key.ProductID,
                    SKU = g.First().Product?.SKU ?? "",
                    ProductName = g.First().Product?.Name ?? "",
                    WarehouseName = g.First().Warehouse?.Name ?? "",
                    AvailableQuantity = g.Where(t => !t.IsReserve).Sum(t => t.Quantity) - Math.Abs(g.Where(t => t.IsReserve).Sum(t => t.Quantity)),
                    ReservedQuantity = Math.Abs(g.Where(t => t.IsReserve).Sum(t => t.Quantity))
                })
                .Where(s => s.TotalQuantity > 0 || s.ReservedQuantity > 0)
                .OrderBy(s => s.ProductName)
                .ToList();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                aggregated = aggregated.Where(s => s.ProductName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || s.SKU.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Stocks.Clear();
                foreach (var item in aggregated) Stocks.Add(item);
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
    private async Task LoadDocumentsAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewInventory)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var docs = await context.InventoryDocuments
                .Include(d => d.Warehouse)
                .AsNoTracking()
                .OrderByDescending(d => d.DocumentDate)
                .Take(100)
                .ToListAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Documents.Clear();
                foreach (var doc in docs) Documents.Add(doc);
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

    partial void OnSearchQueryChanged(string value) => _ = LoadStocksAsync();

    [RelayCommand(CanExecute = nameof(CanAddDocument))]
    private void AddDocument()
    {
        if (_dialogService.ShowInventoryDocumentEditor(null))
        {
            _ = LoadDocumentsAsync();
            _ = LoadStocksAsync();
        }
    }
    private bool CanAddDocument() => _security.HasPermission(AppPermission.EditInventory);

    [RelayCommand(CanExecute = nameof(CanEditDocument))]
    private void EditDocument()
    {
        if (SelectedDocument != null)
        {
            if (_dialogService.ShowInventoryDocumentEditor(SelectedDocument))
            {
                _ = LoadDocumentsAsync();
                _ = LoadStocksAsync();
            }
        }
    }
    private bool CanEditDocument() => SelectedDocument != null && _security.HasPermission(AppPermission.EditInventory);

    // --- ЛОГИКА ОТЧЕТОВ ---

    [RelayCommand]
    private async Task PrintInventoryBalanceAsync()
    {
        if (!DateFromFilter.HasValue || !DateToFilter.HasValue)
        {
            _notify.Warning("Пожалуйста, выберите период (С и По) для формирования Оборотно-сальдовой ведомости.");
            return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var startDate = DateFromFilter.Value.Date;
            var endDate = DateToFilter.Value.Date.AddDays(1).AddTicks(-1);

            var query = await context.InventoryTransactions
                .AsNoTracking()
                .Where(t => t.Timestamp <= endDate && !t.IsReserve && t.Product != null && t.Warehouse != null)
                .GroupBy(t => new { t.WarehouseID, WarehouseName = t.Warehouse!.Name, t.ProductID, t.Product!.SKU, ProductName = t.Product.Name })
                .Select(g => new
                {
                    g.Key.WarehouseName,
                    g.Key.SKU,
                    g.Key.ProductName,
                    Initial = g.Where(x => x.Timestamp < startDate).Sum(x => x.Quantity),
                    In = g.Where(x => x.Timestamp >= startDate && x.Quantity > 0).Sum(x => x.Quantity),
                    Out = Math.Abs(g.Where(x => x.Timestamp >= startDate && x.Quantity < 0).Sum(x => x.Quantity))
                })
                .ToListAsync();

            var data = query
                .Select(x => new InventoryBalanceDto
                {
                    WarehouseName = x.WarehouseName,
                    SKU = x.SKU,
                    ProductName = x.ProductName,
                    InitialBalance = x.Initial,
                    ReceiptQuantity = x.In,
                    ExpenseQuantity = x.Out,
                    FinalBalance = x.Initial + x.In - x.Out
                })
                .Where(x => x.InitialBalance != 0 || x.ReceiptQuantity != 0 || x.ExpenseQuantity != 0 || x.FinalBalance != 0)
                .OrderBy(x => x.WarehouseName).ThenBy(x => x.ProductName)
                .ToList();

            if (!data.Any())
            {
                _notify.Warning("За выбранный период нет данных о движениях и остатках.");
                return;
            }

            var excelBytes = await _inventoryReportService.GenerateInventoryBalanceReportAsync(startDate, endDate, data);

            string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ОСВ_Склад_{startDate:dd.MM.yyyy}-{endDate:dd.MM.yyyy}_{Guid.NewGuid():N}.xlsx");
            await System.IO.File.WriteAllBytesAsync(tempFileName, excelBytes);

            _notify.Success("Оборотно-сальдовая ведомость открыта для предосмотра!");
            new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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
    private async Task PrintDeficitAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var requiredStock = await context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order != null && oi.Order.IsPosted && (oi.Order.Status == "New" || oi.Order.Status == "Planned") && oi.Product != null)
                .GroupBy(oi => new { oi.ProductID, oi.Product!.SKU, ProductName = oi.Product.Name })
                .Select(g => new
                {
                    ProductID = g.Key.ProductID,
                    SKU = g.Key.SKU,
                    ProductName = g.Key.ProductName,
                    Required = g.Sum(x => x.Quantity)
                })
                .ToListAsync();

            var availableStock = await context.InventoryTransactions
                .AsNoTracking()
                .GroupBy(t => t.ProductID)
                .Select(g => new
                {
                    ProductID = g.Key,
                    Available = g.Where(x => !x.IsReserve).Sum(x => x.Quantity) - Math.Abs(g.Where(x => x.IsReserve).Sum(x => x.Quantity))
                })
                .ToDictionaryAsync(x => x.ProductID, x => x.Available);

            var data = new System.Collections.Generic.List<DeficitAnalysisDto>();
            foreach (var req in requiredStock)
            {
                int avail = availableStock.TryGetValue(req.ProductID, out var a) ? a : 0;
                if (req.Required > avail)
                {
                    data.Add(new DeficitAnalysisDto
                    {
                        SKU = req.SKU,
                        ProductName = req.ProductName,
                        RequiredQuantity = req.Required,
                        AvailableQuantity = avail,
                        DeficitQuantity = req.Required - avail
                    });
                }
            }

            data = data.OrderByDescending(x => x.DeficitQuantity).ToList();

            if (!data.Any())
            {
                _notify.Success("Отлично! Дефицита товаров нет. Все заказы обеспечены.");
                return;
            }

            var excelBytes = await _inventoryReportService.GenerateDeficitReportAsync(data);

            string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Анализ_Дефицита_{DateTime.Now:dd.MM.yyyy}_{Guid.NewGuid():N}.xlsx");
            await System.IO.File.WriteAllBytesAsync(tempFileName, excelBytes);

            _notify.Success("Отчет по дефициту открыт для предосмотра!");
            new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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
}
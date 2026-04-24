using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.DTOs;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public partial class OrdersViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;
    private readonly IDialogService _dialogService;
    private readonly IOrderReportService _orderReportService;
    private readonly object _ordersLock = new();

    public ObservableCollection<OrderListDto> Orders { get; } = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private OrderListDto? _selectedOrder;

    [ObservableProperty] private string _highlightText = string.Empty;
    [ObservableProperty] private int _selectedFilterIndex = 0;

    [ObservableProperty] private DateTime? _dateFromFilter;
    [ObservableProperty] private DateTime? _dateToFilter;

    public OrdersViewModel(IDbContextFactory<LogisticsDbContext> dbContextFactory, NotificationService notify, SecurityService security, IDialogService dialogService, IOrderReportService orderReportService)
    {
        _dbContextFactory = dbContextFactory;
        _notify = notify;
        _security = security;
        _dialogService = dialogService;
        _orderReportService = orderReportService;

        BindingOperations.EnableCollectionSynchronization(Orders, _ordersLock);

        // Автоматически загружаем данные при первом создании (Singleton)
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewOrders)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.Orders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(HighlightText))
            {
                var search = HighlightText.ToLower();
                query = query.Where(o =>
                    (o.Customer != null && o.Customer.Name.ToLower().Contains(search)) ||
                    o.OrderID.ToString() == search);
            }

            switch (SelectedFilterIndex)
            {
                case 1: query = query.Where(o => o.Status == "Draft"); break;
                case 2: query = query.Where(o => o.Status == "New"); break;
                case 3: query = query.Where(o => o.Status == "Planned"); break;
                case 4: query = query.Where(o => o.Status == "InTransit"); break;
                case 5: query = query.Where(o => o.Status == "Delivered"); break;
            }

            if (DateFromFilter.HasValue)
            {
                query = query.Where(o => o.OrderDate >= DateFromFilter.Value.Date);
            }
            if (DateToFilter.HasValue)
            {
                var toDate = DateToFilter.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.OrderDate <= toDate);
            }

            var results = await query.Select(o => new OrderListDto
            {
                OrderID = o.OrderID,
                OrderDate = o.OrderDate,
                CustomerName = o.Customer != null ? o.Customer.Name : "Неизвестно",
                Customer = o.Customer,
                WarehouseName = o.Warehouse != null ? o.Warehouse.Name : "-",
                TotalSum = o.Items.Sum(i => i.TotalPrice),
                WeightKG = o.WeightKG,
                RequiredTempMode = o.RequiredTempMode,
                Status = o.Status,
                IsPosted = o.IsPosted
            }).OrderByDescending(o => o.OrderDate).ToListAsync();

            lock (_ordersLock)
            {
                Orders.Clear();
                foreach (var r in results) Orders.Add(r);
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

    partial void OnHighlightTextChanged(string value) => _ = LoadDataAsync();
    partial void OnSelectedFilterIndexChanged(int value) => _ = LoadDataAsync();
    partial void OnDateFromFilterChanged(DateTime? value) => _ = LoadDataAsync();
    partial void OnDateToFilterChanged(DateTime? value) => _ = LoadDataAsync();

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (_dialogService.ShowOrderEditor(null)) _ = LoadDataAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndEditPermission))]
    private async Task EditAsync()
    {
        if (SelectedOrder == null) return;
        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var fullOrder = await context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Warehouse)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderID == SelectedOrder.OrderID);

            if (fullOrder != null && _dialogService.ShowOrderEditor(fullOrder))
            {
                await LoadDataAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndDeletePermission))]
    private async Task DeleteAsync()
    {
        if (SelectedOrder == null) return;
        if (_dialogService.ShowConfirmation("Подтверждение", $"Удалить заказ №{SelectedOrder.OrderID}?"))
        {
            IsLoading = true;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var orderToDelete = await context.Orders.FindAsync(SelectedOrder.OrderID);
                if (orderToDelete != null)
                {
                    if (orderToDelete.IsPosted)
                    {
                        _notify.Error("Нельзя удалить проведенный заказ. Сначала отмените проведение.");
                        return;
                    }
                    context.Orders.Remove(orderToDelete);
                    await context.SaveChangesAsync();
                    _notify.Success("Заказ успешно удален");
                    await LoadDataAsync();
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
    }

    [RelayCommand]
    private async Task PrintOrdersReportAsync()
    {
        if (!DateFromFilter.HasValue || !DateToFilter.HasValue)
        {
            _notify.Warning("Пожалуйста, выберите период (С и По) вручную для формирования отчета.");
            return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            var toDate = DateToFilter.Value.Date.AddDays(1).AddTicks(-1);

            var orders = await context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.OrderDate >= DateFromFilter.Value.Date && o.OrderDate <= toDate && o.IsPosted)
                .ToListAsync();

            if (!orders.Any())
            {
                _notify.Warning("За выбранный период нет проведенных заказов.");
                return;
            }

            var groupedData = orders
                .GroupBy(o => o.Customer?.Name ?? "Неизвестный контрагент")
                .Select(g => new CustomerOrdersGroupDto
                {
                    CustomerName = g.Key,
                    TotalGroupSum = g.Sum(o => o.Items.Sum(i => i.TotalPrice)),
                    TotalGroupWeight = (decimal)g.Sum(o => o.WeightKG),
                    Items = g.SelectMany(o => o.Items.Select(i => new OrderReportItemDto
                    {
                        OrderID = o.OrderID,
                        OrderDate = o.OrderDate,
                        ProductSKU = i.Product?.SKU ?? "",
                        ProductName = i.Product?.Name ?? "",
                        Quantity = (decimal)i.Quantity,
                        Price = (decimal)i.Price,
                        TotalSum = (decimal)i.TotalPrice,
                        TotalWeight = (decimal)i.TotalWeight
                    })).ToList()
                }).ToList();

            var excelBytes = await _orderReportService.GenerateOrdersByCustomerReportAsync(DateFromFilter.Value, DateToFilter.Value, groupedData);

            string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Отчет_по_заказам_{DateFromFilter.Value:dd.MM.yyyy}-{DateToFilter.Value:dd.MM.yyyy}_{Guid.NewGuid():N}.xlsx");
            await System.IO.File.WriteAllBytesAsync(tempFileName, excelBytes);

            _notify.Success("Отчет по заказам открыт для предосмотра!");
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

    private bool CanAdd() => _security.HasPermission(AppPermission.EditOrders);
    private bool HasSelectionAndEditPermission() => SelectedOrder != null && _security.HasPermission(AppPermission.EditOrders);
    private bool HasSelectionAndDeletePermission() => SelectedOrder != null && _security.HasPermission(AppPermission.DeleteOrders);
}
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

public partial class OrderDebtInfo : ObservableObject
{
    public int OrderID { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingDebt { get; set; }
    public string DisplayText => OrderID == -1
        ? "Авансовый платеж (Без привязки к заказу)"
        : $"Заказ №{OrderID} от {OrderDate:dd.MM.yyyy} (Остаток: {RemainingDebt:N2} ₽)";
}

public sealed partial class PaymentDocumentEditorViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;

    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<Customer> _availableCustomers = new();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Контрагент обязателен")]
    private Customer? _selectedCustomer;

    [ObservableProperty] private ObservableCollection<OrderDebtInfo> _availableOrders = new();

    [ObservableProperty]
    private OrderDebtInfo? _selectedOrder;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.01, 1000000000.0, ErrorMessage = "Сумма платежа должна быть больше нуля")]
    private decimal _amount;

    [ObservableProperty] private DateTime _paymentDate = DateTime.Now;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Назначение платежа обязательно")]
    private string _description = string.Empty;

    public PaymentDocumentEditorViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, SecurityService security)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _security = security;
    }

    public void Initialize()
    {
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var customers = await context.Customers.OrderBy(c => c.Name).AsNoTracking().ToListAsync().ConfigureAwait(false);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableCustomers.Clear();
                foreach (var c in customers) AvailableCustomers.Add(c);
                PaymentDate = DateTime.Now;
                Amount = 0;
                Description = string.Empty;
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

    async partial void OnSelectedCustomerChanged(Customer? value)
    {
        if (value == null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => AvailableOrders.Clear());
            return;
        }
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

            var debts = await context.MutualSettlements
                .Where(ms => ms.CustomerID == value.CustomerID)
                .GroupBy(ms => ms.OrderID)
                .Select(g => new
                {
                    OrderID = g.Key,
                    TotalDebt = g.Where(x => x.Type == MutualSettlementType.DebtIncrease).Sum(x => x.Amount),
                    TotalPaid = g.Where(x => x.Type == MutualSettlementType.DebtDecrease).Sum(x => x.Amount)
                })
                .Where(x => x.OrderID != null && x.TotalDebt > x.TotalPaid)
                .ToListAsync()
                .ConfigureAwait(false);

            var activeOrderIds = debts.Select(d => d.OrderID).ToList();
            var ordersInfo = await context.Orders
                .Where(o => activeOrderIds.Contains(o.OrderID))
                .Select(o => new { o.OrderID, o.OrderDate })
                .ToDictionaryAsync(o => o.OrderID, o => o.OrderDate)
                .ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableOrders.Clear();
                AvailableOrders.Add(new OrderDebtInfo { OrderID = -1, RemainingDebt = 0 });

                foreach (var d in debts)
                {
                    if (d.OrderID.HasValue && ordersInfo.TryGetValue(d.OrderID.Value, out var date))
                    {
                        AvailableOrders.Add(new OrderDebtInfo
                        {
                            OrderID = d.OrderID.Value,
                            OrderDate = date,
                            TotalAmount = d.TotalDebt,
                            PaidAmount = d.TotalPaid,
                            RemainingDebt = d.TotalDebt - d.TotalPaid
                        });
                    }
                }
                SelectedOrder = AvailableOrders.First();
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

    partial void OnSelectedOrderChanged(OrderDebtInfo? value)
    {
        if (value == null) return;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (value.OrderID == -1)
            {
                Amount = 0;
                Description = "Авансовый платеж по договору";
            }
            else
            {
                Amount = value.RemainingDebt;
                Description = $"Оплата по заказу №{value.OrderID} от {value.OrderDate:dd.MM.yyyy}";
            }
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors || SelectedCustomer == null)
        {
            _notify.Warning("Пожалуйста, корректно заполните все обязательные поля");
            return;
        }

        if (SelectedOrder != null && SelectedOrder.OrderID != -1 && Amount > SelectedOrder.RemainingDebt)
        {
            _notify.Error($"Блокировка: Сумма платежа превышает остаток долга по заказу ({SelectedOrder.RemainingDebt:N2} ₽). Оформите излишек как аванс без привязки к заказу.");
            return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

            int? targetOrderId = (SelectedOrder != null && SelectedOrder.OrderID != -1) ? SelectedOrder.OrderID : null;

            var settlement = new MutualSettlement
            {
                CustomerID = SelectedCustomer.CustomerID,
                OrderID = targetOrderId,
                Date = PaymentDate,
                Amount = Amount,
                Type = MutualSettlementType.DebtDecrease,
                Description = Description
            };

            context.MutualSettlements.Add(settlement);

            var auditLog = new AuditLog
            {
                Action = "Проведение платежа",
                EntityName = "Казначейство",
                Details = $"Принят платеж {Amount:N2} ₽ от {SelectedCustomer.Name}. Привязка к заказу: {(targetOrderId.HasValue ? targetOrderId.Value.ToString() : "Нет")}",
                Timestamp = DateTime.Now,
                UserID = _security.CurrentUser?.UserID
            };

            context.AuditLogs.Add(auditLog);
            await context.SaveChangesAsync().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success("Платеж (ПКО) успешно проведен в системе. Взаиморасчеты пересчитаны.");
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
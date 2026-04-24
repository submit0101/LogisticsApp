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
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public sealed partial class FinanceViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly IDialogService _dialogService;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;
    private readonly IFinanceReportService _financeReportService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private decimal _totalReceivables;
    [ObservableProperty] private decimal _totalAdvances;

    // Даты для формирования отчетов
    [ObservableProperty] private DateTime? _dateFromFilter = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _dateToFilter = DateTime.Today;

    public ObservableCollection<CustomerDebtDto> Debts { get; } = new();
    public ObservableCollection<MutualSettlement> Payments { get; } = new();

    [ObservableProperty] private CustomerDebtDto? _selectedDebt;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddPaymentCommand))]
    private MutualSettlement? _selectedPayment;

    public FinanceViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        IDialogService dialogService,
        NotificationService notify,
        SecurityService security,
        IFinanceReportService financeReportService)
    {
        _dbFactory = dbFactory;
        _dialogService = dialogService;
        _notify = notify;
        _security = security;
        _financeReportService = financeReportService;
    }

    public async Task InitializeAsync()
    {
        await LoadDebtsAsync();
        await LoadPaymentsAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadDebtsAsync();
    }

    [RelayCommand]
    private async Task LoadDebtsAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewFinance)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var settlements = await context.MutualSettlements
                .Include(ms => ms.Customer)
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);

            var aggregated = settlements
                .Where(ms => ms.Customer != null)
                .GroupBy(ms => new { ms.CustomerID, ms.Customer!.Name, ms.Customer.INN })
                .Select(g => new CustomerDebtDto
                {
                    CustomerID = g.Key.CustomerID,
                    CustomerName = g.Key.Name,
                    INN = g.Key.INN,
                    TotalShipped = g.Where(x => x.Type == MutualSettlementType.DebtIncrease).Sum(x => x.Amount),
                    TotalPaid = g.Where(x => x.Type == MutualSettlementType.DebtDecrease).Sum(x => x.Amount)
                })
                .OrderByDescending(d => d.CurrentDebt)
                .ToList();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                aggregated = aggregated.Where(d =>
                    d.CustomerName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    d.INN.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Debts.Clear();
                foreach (var item in aggregated) Debts.Add(item);

                TotalReceivables = Debts.Sum(d => d.CurrentDebt);
                TotalAdvances = Debts.Sum(d => d.AdvanceAmount);
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
    private async Task LoadPaymentsAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewFinance)) return;
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var payments = await context.MutualSettlements
                .Include(ms => ms.Customer)
                .Where(ms => ms.Type == MutualSettlementType.DebtDecrease)
                .AsNoTracking()
                .OrderByDescending(ms => ms.Date)
                .Take(200)
                .ToListAsync()
                .ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Payments.Clear();
                foreach (var p in payments) Payments.Add(p);
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
    private async Task SearchAsync() => await LoadDebtsAsync();

    [RelayCommand(CanExecute = nameof(CanAddPayment))]
    private void AddPayment()
    {
        if (_dialogService.ShowPaymentDocumentEditor(null))
        {
            _ = LoadDebtsAsync();
            _ = LoadPaymentsAsync();
        }
    }

    private bool CanAddPayment() => _security.HasPermission(AppPermission.EditFinance);


    [RelayCommand]
    private async Task PrintReconciliationAsync()
    {
        if (SelectedDebt == null)
        {
            _notify.Warning("Для формирования Акта сверки выберите контрагента в таблице 'Взаиморасчеты (Долги)'.");
            return;
        }

        if (!DateFromFilter.HasValue || !DateToFilter.HasValue)
        {
            _notify.Warning("Укажите период (С и По) для формирования отчета.");
            return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var startDate = DateFromFilter.Value.Date;
            var endDate = DateToFilter.Value.Date.AddDays(1).AddTicks(-1);
            var customerId = SelectedDebt.CustomerID;

            // Все транзакции по клиенту
            var allTxs = await context.MutualSettlements
                .AsNoTracking()
                .Where(ms => ms.CustomerID == customerId)
                .ToListAsync()
                .ConfigureAwait(false);

            var pastTxs = allTxs.Where(t => t.Date < startDate).ToList();
            decimal initialBalance = pastTxs.Where(t => t.Type == MutualSettlementType.DebtIncrease).Sum(t => t.Amount) -
                                     pastTxs.Where(t => t.Type == MutualSettlementType.DebtDecrease).Sum(t => t.Amount);

            var periodTxs = allTxs.Where(t => t.Date >= startDate && t.Date <= endDate).OrderBy(t => t.Date).ToList();

            var items = new System.Collections.Generic.List<ReconciliationItemDto>();
            decimal currentBalance = initialBalance;

            foreach (var tx in periodTxs)
            {
                decimal increase = tx.Type == MutualSettlementType.DebtIncrease ? tx.Amount : 0;
                decimal decrease = tx.Type == MutualSettlementType.DebtDecrease ? tx.Amount : 0;

                currentBalance = currentBalance + increase - decrease;

                items.Add(new ReconciliationItemDto
                {
                    Date = tx.Date,
                    Document = tx.Description,
                    DebtIncrease = increase,
                    DebtDecrease = decrease
                });
            }

            var reportData = new CustomerReconciliationDto
            {
                CustomerName = SelectedDebt.CustomerName,
                INN = SelectedDebt.INN,
                InitialBalance = initialBalance,
                Items = items,
                FinalBalance = currentBalance
            };

            var excelBytes = await _financeReportService.GenerateReconciliationReportAsync(startDate, endDate, reportData).ConfigureAwait(false);

            string safeName = System.Text.RegularExpressions.Regex.Replace(SelectedDebt.CustomerName, @"[^a-zA-Zа-яА-Я0-9_]", "_");
            string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Акт_Сверки_{safeName}_{DateTime.Now:ddMMyyyy_HHmm}.xlsx");

            await System.IO.File.WriteAllBytesAsync(tempFileName, excelBytes).ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success("Акт сверки успешно сформирован!");
                new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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
    private async Task PrintCashFlowAsync()
    {
        if (!DateFromFilter.HasValue || !DateToFilter.HasValue)
        {
            _notify.Warning("Укажите период (С и По) для формирования отчета.");
            return;
        }

        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            var startDate = DateFromFilter.Value.Date;
            var endDate = DateToFilter.Value.Date.AddDays(1).AddTicks(-1);

            var payments = await context.MutualSettlements
                .Include(ms => ms.Customer)
                .AsNoTracking()
                .Where(ms => ms.Date >= startDate && ms.Date <= endDate && ms.Type == MutualSettlementType.DebtDecrease)
                .OrderBy(ms => ms.Date)
                .ToListAsync()
                .ConfigureAwait(false);

            if (!payments.Any())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => _notify.Warning("За выбранный период поступлений не найдено."));
                return;
            }

            var data = payments.Select(p => new CashFlowReportDto
            {
                PaymentDate = p.Date,
                CustomerName = p.Customer?.Name ?? "Неизвестно",
                Amount = p.Amount,
                Description = p.Description
            }).ToList();

            var excelBytes = await _financeReportService.GenerateCashFlowReportAsync(startDate, endDate, data).ConfigureAwait(false);

            string tempFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Реестр_ДДС_{startDate:ddMM}-{endDate:ddMM}_{Guid.NewGuid():N}.xlsx");
            await System.IO.File.WriteAllBytesAsync(tempFileName, excelBytes).ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notify.Success("Реестр поступлений успешно сформирован!");
                new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(tempFileName) { UseShellExecute = true } }.Start();
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
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Wpf;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public sealed partial class ReportsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbContextFactory;
    private readonly IReportDataService _reportDataService;
    private readonly NotificationService _notify;
    private readonly OverlayService _overlay;
    private readonly ExcelExportService _exportService;
    private readonly ILogisticsAnalyticsReportService _analyticsService;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTime _dateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;

    public IReadOnlyList<string> ReportTypes { get; } =
    [
        "Реестр путевых листов",
        "Аналитика Тонно-километры",
        "Аналитика Пробег ТС",
        "Аналитика Показания одометров"
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaybillsGridVisible))]
    [NotifyPropertyChangedFor(nameof(IsTonKmGridVisible))]
    [NotifyPropertyChangedFor(nameof(IsMileageGridVisible))]
    [NotifyPropertyChangedFor(nameof(IsOdometerGridVisible))]
    private string _selectedReportType = "Реестр путевых листов";

    public bool IsWaybillsGridVisible => SelectedReportType == "Реестр путевых листов";
    public bool IsTonKmGridVisible => SelectedReportType == "Аналитика Тонно-километры";
    public bool IsMileageGridVisible => SelectedReportType == "Аналитика Пробег ТС";
    public bool IsOdometerGridVisible => SelectedReportType == "Аналитика Показания одометров";

    [ObservableProperty] private ICollectionView? _currentReportView;

    [ObservableProperty] private string _grandTotal1 = string.Empty;
    [ObservableProperty] private string _grandTotal2 = string.Empty;
    [ObservableProperty] private string _grandTotal3 = string.Empty;

    [ObservableProperty] private SeriesCollection _mainChartSeries = [];
    [ObservableProperty] private List<string> _chartLabels = [];
    [ObservableProperty] private SeriesCollection _pieChartSeries = [];

    public ReportsViewModel(
        IDbContextFactory<LogisticsDbContext> dbContextFactory,
        IReportDataService reportDataService,
        NotificationService notify,
        OverlayService overlay,
        ExcelExportService exportService,
        ILogisticsAnalyticsReportService analyticsService)
    {
        _dbContextFactory = dbContextFactory;
        _reportDataService = reportDataService;
        _notify = notify;
        _overlay = overlay;
        _exportService = exportService;
        _analyticsService = analyticsService;
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (DateFrom > DateTo)
        {
            _notify.Warning("Дата начала не может быть больше даты окончания.");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsLoading = true;

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            try
            {
                if (SelectedReportType == "Реестр путевых листов")
                {
                    var data = await _reportDataService.GetWaybillsRegistryAsync(DateFrom, DateTo, token).ConfigureAwait(false);
                    Application.Current.Dispatcher.Invoke(() => BuildWaybillsReport(data));
                }
                else if (SelectedReportType == "Аналитика Тонно-километры")
                {
                    var data = await GetTonKilometersDataAsync(token).ConfigureAwait(false);
                    Application.Current.Dispatcher.Invoke(() => BuildTonKmReport(data));
                }
                else if (SelectedReportType == "Аналитика Пробег ТС")
                {
                    var data = await GetMileageDataAsync(token).ConfigureAwait(false);
                    Application.Current.Dispatcher.Invoke(() => BuildMileageReport(data));
                }
                else if (SelectedReportType == "Аналитика Показания одометров")
                {
                    var data = await GetOdometerDataAsync(token).ConfigureAwait(false);
                    Application.Current.Dispatcher.Invoke(() => BuildOdometerReport(data));
                }

                Application.Current.Dispatcher.Invoke(() => _notify.Success("Отчет успешно сформирован на экране"));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error($"Сбой формирования отчета: {ex.Message}"));
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() => IsLoading = false);
            }
        }, "Формирование куба данных и агрегация...");
    }

    [RelayCommand]
    private void ExportReport()
    {
        if (CurrentReportView == null || CurrentReportView.IsEmpty)
        {
            _notify.Warning("Нет данных для экспорта. Сначала сформируйте отчет.");
            return;
        }

        var source = CurrentReportView.SourceCollection;

        // РЕШЕНИЕ ПРОБЛЕМЫ ФАЙЛОВ: Очистка имени файла от запрещенных символов Windows
        string safeName = Regex.Replace(SelectedReportType, @"[^a-zA-Zа-яА-Я0-9_]", "_");

        if (source is IEnumerable<WaybillRegistryDto> w) _exportService.Export(w, safeName);
        else if (source is IEnumerable<TonKilometerReportDto> t) _exportService.Export(t, safeName);
        else if (source is IEnumerable<MileageReportDto> m) _exportService.Export(m, safeName);
        else if (source is IEnumerable<OdometerReportDto> o) _exportService.Export(o, safeName);
    }

    [RelayCommand]
    private void CancelGeneration()
    {
        _cts?.Cancel();
    }

    // --- СБОРЩИКИ ДАННЫХ ДЛЯ ОТЧЕТОВ ---
    private async Task<List<TonKilometerReportDto>> GetTonKilometersDataAsync(CancellationToken ct)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var endPeriod = DateTo.Date.AddDays(1).AddTicks(-1);

        var waybills = await context.Waybills
            .Include(w => w.Vehicle)
            .Include(w => w.Driver)
            .Include(w => w.Points)
                .ThenInclude(p => p.Order)
            .Include(w => w.FuelTickets)
            .Where(w => w.DateOut >= DateFrom.Date && w.DateOut <= endPeriod && w.IsPosted)
            .AsNoTracking() // Ускорение: не отслеживаем сущности в EF
            .ToListAsync(ct).ConfigureAwait(false);

        return waybills.Select(w =>
        {
            decimal totalWeight = (decimal)w.Points.Where(p => p.Order != null).Sum(p => p.Order!.WeightKG) / 1000m;
            decimal loadedMlg = (decimal)((w.OdometerIn ?? 0) - (w.OdometerOut ?? 0)) * 0.5m;

            return new TonKilometerReportDto
            {
                VehicleName = w.Vehicle?.Model ?? "Неизвестно",
                DriverName = w.Driver?.FullName ?? "Неизвестно",
                DateOut = w.DateOut ?? DateTime.Now,
                DateIn = w.DateIn ?? w.DateOut ?? DateTime.Now,
                TotalMileage = (decimal)((w.OdometerIn ?? 0) - (w.OdometerOut ?? 0)),
                LoadedMileage = loadedMlg,
                TotalWeightTons = totalWeight,
                TonKilometers = totalWeight * loadedMlg,
                WorkHours = w.DateIn.HasValue && w.DateOut.HasValue ? (decimal)(w.DateIn.Value - w.DateOut.Value).TotalHours : 0,
                HeaterHours = 0,
                RefrigeratorHours = 0,
                FuelRefueled = (decimal)w.FuelTickets.Sum(f => f.VolumeLiters)
            };
        }).OrderBy(x => x.DateOut).ToList();
    }

    // РЕШЕНИЕ ПРОБЛЕМЫ ЛАГОВ: Полностью переписаны функции получения пробегов
    // Теперь вместо 300+ запросов к БД выполняется всего 3 быстрых групповых (SQL GroupBy)
    private async Task<List<MileageReportDto>> GetMileageDataAsync(CancellationToken ct)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var endPeriod = DateTo.Date.AddDays(1).AddTicks(-1);

        var vehicles = await context.Vehicles.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

        // 1 запрос: агрегируем пробег за период для всех авто сразу
        var periodMileages = await context.Waybills
            .Where(w => w.DateOut >= DateFrom.Date && w.DateOut <= endPeriod && w.IsPosted && w.VehicleID != null)
            .GroupBy(w => w.VehicleID!.Value)
            .Select(g => new { VehicleID = g.Key, Mileage = g.Sum(w => (w.OdometerIn ?? 0) - (w.OdometerOut ?? 0)) })
            .ToDictionaryAsync(x => x.VehicleID, x => x.Mileage, ct)
            .ConfigureAwait(false);

        // 2 запрос: агрегируем общий пробег для всех авто сразу
        var totalMileages = await context.Waybills
            .Where(w => w.IsPosted && w.VehicleID != null)
            .GroupBy(w => w.VehicleID!.Value)
            .Select(g => new { VehicleID = g.Key, Mileage = g.Sum(w => (w.OdometerIn ?? 0) - (w.OdometerOut ?? 0)) })
            .ToDictionaryAsync(x => x.VehicleID, x => x.Mileage, ct)
            .ConfigureAwait(false);

        var data = new List<MileageReportDto>();

        foreach (var v in vehicles)
        {
            periodMileages.TryGetValue(v.VehicleID, out var periodMileage);
            totalMileages.TryGetValue(v.VehicleID, out var totalMileage);

            if (periodMileage > 0 || totalMileage > 0)
            {
                data.Add(new MileageReportDto
                {
                    VehicleRegNumber = v.RegNumber,
                    VehicleModel = v.Model,
                    PeriodMileage = (decimal)periodMileage,
                    TotalMileageSinceStart = (decimal)totalMileage
                });
            }
        }
        return data.OrderByDescending(d => d.PeriodMileage).ToList();
    }

    private async Task<List<OdometerReportDto>> GetOdometerDataAsync(CancellationToken ct)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var endPeriod = DateTo.Date.AddDays(1).AddTicks(-1);

        var vehicles = await context.Vehicles.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

        // Вытягиваем сразу все рейсы за период для всех ТС
        var periodWaybills = await context.Waybills
            .Where(w => w.DateOut >= DateFrom.Date && w.DateOut <= endPeriod && w.IsPosted && w.VehicleID != null)
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var groupedPeriodWaybills = periodWaybills.GroupBy(w => w.VehicleID!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var totalMileages = await context.Waybills
            .Where(w => w.IsPosted && w.VehicleID != null)
            .GroupBy(w => w.VehicleID!.Value)
            .Select(g => new { VehicleID = g.Key, Mileage = g.Sum(w => (w.OdometerIn ?? 0) - (w.OdometerOut ?? 0)) })
            .ToDictionaryAsync(x => x.VehicleID, x => x.Mileage, ct)
            .ConfigureAwait(false);

        var data = new List<OdometerReportDto>();

        foreach (var v in vehicles)
        {
            if (groupedPeriodWaybills.TryGetValue(v.VehicleID, out var waybillsInPeriod) && waybillsInPeriod.Any())
            {
                var firstWaybill = waybillsInPeriod.OrderBy(w => w.DateOut).First();
                var lastWaybill = waybillsInPeriod.OrderByDescending(w => w.DateIn).First();
                var periodMileage = waybillsInPeriod.Sum(w => (w.OdometerIn ?? 0) - (w.OdometerOut ?? 0));
                totalMileages.TryGetValue(v.VehicleID, out var totalMileage);

                data.Add(new OdometerReportDto
                {
                    VehicleRegNumber = v.RegNumber,
                    VehicleModel = v.Model,
                    OdometerStart = (decimal)(firstWaybill.OdometerOut ?? 0),
                    PeriodMileage = (decimal)periodMileage,
                    TotalMileageSinceStart = (decimal)totalMileage,
                    OdometerEnd = (decimal)(lastWaybill.OdometerIn ?? 0)
                });
            }
        }
        return data.OrderByDescending(d => d.PeriodMileage).ToList();
    }

    // --- ПОСТРОИТЕЛИ ИНТЕРФЕЙСА ---

    private void ClearCharts()
    {
        MainChartSeries.Clear();
        ChartLabels.Clear();
        PieChartSeries.Clear();
        GrandTotal1 = "-";
        GrandTotal2 = "-";
        GrandTotal3 = "-";
        CurrentReportView = null;
    }

    private void BuildWaybillsReport(List<WaybillRegistryDto> data)
    {
        if (!data.Any())
        {
            ClearCharts();
            _notify.Warning("Нет данных за выбранный период.");
            return;
        }

        var view = CollectionViewSource.GetDefaultView(data);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(WaybillRegistryDto.DriverName)));
        CurrentReportView = view;

        GrandTotal1 = $"Рейсов: {data.Count}";
        GrandTotal2 = $"Дистанция: {data.Sum(x => x.Distance):N1} км";
        GrandTotal3 = $"Тоннаж: {data.Sum(x => x.TotalWeight):N0} кг";

        MainChartSeries = [
            new ColumnSeries { Title = "Рейсы", Values = new ChartValues<int>(data.GroupBy(d => d.DateCreate.Date).Select(g => g.Count())) }
        ];
        ChartLabels = data.GroupBy(d => d.DateCreate.Date).Select(g => g.Key.ToString("dd.MM")).ToList();

        PieChartSeries = [
            new PieSeries { Title = "В рейсе", Values = new ChartValues<int> { data.Count(d => d.Status == "Active") }, DataLabels = true },
            new PieSeries { Title = "Завершены", Values = new ChartValues<int> { data.Count(d => d.Status == "Completed") }, DataLabels = true },
            new PieSeries { Title = "Отменены", Values = new ChartValues<int> { data.Count(d => d.Status == "Cancelled") }, DataLabels = true }
        ];
    }

    private void BuildTonKmReport(List<TonKilometerReportDto> data)
    {
        if (!data.Any())
        {
            ClearCharts();
            _notify.Warning("Нет данных за выбранный период.");
            return;
        }

        var view = CollectionViewSource.GetDefaultView(data);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TonKilometerReportDto.VehicleName)));
        CurrentReportView = view;

        GrandTotal1 = $"ТС: {data.Select(d => d.VehicleName).Distinct().Count()}";
        GrandTotal2 = $"Тн*км: {data.Sum(x => x.TonKilometers):N1}";
        GrandTotal3 = $"Пробег: {data.Sum(x => x.TotalMileage):N0} км";

        var grouped = data.GroupBy(d => d.VehicleName).ToList();
        MainChartSeries = [
            new ColumnSeries { Title = "Тн*км", Values = new ChartValues<double>(grouped.Select(g => (double)g.Sum(x => x.TonKilometers))) }
        ];
        ChartLabels = grouped.Select(g => g.Key).ToList();

        var pieSeries = new SeriesCollection();
        foreach (var g in grouped.Take(5))
        {
            pieSeries.Add(new PieSeries { Title = g.Key, Values = new ChartValues<double> { (double)g.Sum(x => x.FuelRefueled) }, DataLabels = true });
        }
        PieChartSeries = pieSeries;
    }

    private void BuildMileageReport(List<MileageReportDto> data)
    {
        if (!data.Any())
        {
            ClearCharts();
            _notify.Warning("Нет данных за выбранный период.");
            return;
        }

        var view = CollectionViewSource.GetDefaultView(data);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MileageReportDto.VehicleModel)));
        CurrentReportView = view;

        GrandTotal1 = $"Моделей ТС: {data.Select(d => d.VehicleModel).Distinct().Count()}";
        GrandTotal2 = $"Пробег (период): {data.Sum(x => x.PeriodMileage):N0} км";
        GrandTotal3 = $"Пробег (общий): {data.Sum(x => x.TotalMileageSinceStart):N0} км";

        var top = data.OrderByDescending(d => d.PeriodMileage).Take(10).ToList();
        MainChartSeries = [
            new ColumnSeries { Title = "Пробег за период (км)", Values = new ChartValues<double>(top.Select(d => (double)d.PeriodMileage)) }
        ];
        ChartLabels = top.Select(d => d.VehicleRegNumber).ToList();

        var pieSeries = new SeriesCollection();
        foreach (var d in top.Take(5))
        {
            pieSeries.Add(new PieSeries { Title = d.VehicleRegNumber, Values = new ChartValues<double> { (double)d.TotalMileageSinceStart }, DataLabels = true });
        }
        PieChartSeries = pieSeries;
    }

    private void BuildOdometerReport(List<OdometerReportDto> data)
    {
        if (!data.Any())
        {
            ClearCharts();
            _notify.Warning("Нет данных за выбранный период.");
            return;
        }

        var view = CollectionViewSource.GetDefaultView(data);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(OdometerReportDto.VehicleModel)));
        CurrentReportView = view;

        GrandTotal1 = $"ТС: {data.Count}";
        GrandTotal2 = $"Ср. пробег/мес: {(data.Count > 0 ? data.Average(x => x.PeriodMileage) : 0):N0} км";
        GrandTotal3 = $"Пробег (общий): {data.Sum(x => x.TotalMileageSinceStart):N0} км";

        var top = data.OrderByDescending(d => d.PeriodMileage).Take(10).ToList();
        MainChartSeries = [
            new ColumnSeries { Title = "Пробег за период", Values = new ChartValues<double>(top.Select(d => (double)d.PeriodMileage)) }
        ];
        ChartLabels = top.Select(d => d.VehicleRegNumber).ToList();

        var pieSeries = new SeriesCollection();
        foreach (var d in top.Take(5))
        {
            pieSeries.Add(new PieSeries { Title = d.VehicleRegNumber, Values = new ChartValues<double> { (double)d.TotalMileageSinceStart }, DataLabels = true });
        }
        PieChartSeries = pieSeries;
    }
}
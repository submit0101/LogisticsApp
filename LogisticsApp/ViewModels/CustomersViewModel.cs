using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace LogisticsApp.ViewModels;

public partial class CustomersViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly SecurityService _security;
    private readonly OverlayService _overlay;
    private readonly ExcelImportService _importService;
    private readonly IDialogService _dialogService;
    private readonly DaDataService _daDataService;
    private readonly GeocodingService _geoService;

    private List<Customer> _allCustomers = new();

    [ObservableProperty]
    private ObservableCollection<Customer> _customers = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private string _highlightText = string.Empty;

    [ObservableProperty]
    private int _selectedTypeFilterIndex = 0;

    public CustomersViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        SecurityService security,
        OverlayService overlay,
        ExcelImportService importService,
        IDialogService dialogService,
        DaDataService daDataService,
        GeocodingService geoService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _security = security;
        _overlay = overlay;
        _importService = importService;
        _dialogService = dialogService;
        _daDataService = daDataService;
        _geoService = geoService;

        _ = LoadDataAsync();
    }

    partial void OnSelectedTypeFilterIndexChanged(int value) => ApplyFilters();

    partial void OnHighlightTextChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewCustomers)) return;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            _allCustomers = await context.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allCustomers.AsEnumerable();

        switch (SelectedTypeFilterIndex)
        {
            case 1: filtered = filtered.Where(c => c.Type == CustomerType.LegalEntity); break;
            case 2: filtered = filtered.Where(c => c.Type == CustomerType.Entrepreneur); break;
            case 3: filtered = filtered.Where(c => c.Type == CustomerType.PhysicalPerson); break;
        }

        if (!string.IsNullOrWhiteSpace(HighlightText))
        {
            var search = HighlightText.ToLower();
            filtered = filtered.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(search)) ||
                (c.INN != null && c.INN.ToLower().Contains(search)) ||
                (c.Address != null && c.Address.ToLower().Contains(search)));
        }

        Customers = new ObservableCollection<Customer>(filtered);
    }

    [RelayCommand]
    private void Refresh()
    {
        _ = LoadDataAsync();
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (_dialogService.ShowCustomerEditor(null)) _ = LoadDataAsync();
    }

    private bool CanAdd() => _security.HasPermission(AppPermission.EditCustomers);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit()
    {
        if (SelectedCustomer != null && _dialogService.ShowCustomerEditor(SelectedCustomer))
            _ = LoadDataAsync();
    }

    private bool CanEdit() => SelectedCustomer != null && _security.HasPermission(AppPermission.EditCustomers);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedCustomer == null) return;
        if (_dialogService.ShowConfirmation("Подтверждение", $"Пометить на удаление '{SelectedCustomer.Name}'?"))
        {
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();
                if (await context.Orders.AnyAsync(o => o.CustomerID == SelectedCustomer.CustomerID))
                {
                    _notify.Warning("Нельзя удалить клиента с историей заказов.");
                    return;
                }
                var customerToDelete = await context.Customers.FindAsync(SelectedCustomer.CustomerID);
                if (customerToDelete != null)
                {
                    context.Customers.Remove(customerToDelete);
                    await context.SaveChangesAsync();
                    _notify.Success("Клиент помечен на удаление");
                    await LoadDataAsync();
                }
            }
            catch (Exception ex) { _notify.Error(ex.Message); }
        }
    }

    private bool CanDelete() => SelectedCustomer != null && _security.HasPermission(AppPermission.DeleteCustomers);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task ImportExcelAsync()
    {
        var openFileDialog = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls" };
        if (openFileDialog.ShowDialog() != true) return;
        string filePath = openFileDialog.FileName;

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            var result = await _importService.ImportCustomersAsync(filePath);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (result.Errors > 0 && result.Added == 0 && result.Updated == 0)
                {
                    _notify.Error($"Сбой импорта.\n{result.ErrorDetails}");
                }
                else
                {
                    string message = $"Импорт завершен.\nДобавлено: {result.Added}\nОбновлено: {result.Updated}";
                    if (result.Errors > 0)
                    {
                        message += $"\nОшибок: {result.Errors}. Подробности в логе.";
                        _notify.Warning(message);
                        Serilog.Log.Warning("Ошибки при импорте Excel: \n" + result.ErrorDetails);
                    }
                    else
                    {
                        _notify.Success(message);
                    }
                    _ = LoadDataAsync();
                }
            });
        }, "Чтение и импорт файла Excel...");
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewCustomers)) return;

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"Справочник_Контрагентов_{DateTime.Now:dd_MM_yyyy}.xlsx"
        };

        if (saveFileDialog.ShowDialog() != true) return;
        string filePath = saveFileDialog.FileName;

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            var result = await _importService.ExportCustomersAsync(filePath);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (result)
                    _notify.Success("База контрагентов успешно выгружена в Excel!");
                else
                    _notify.Error("Произошла ошибка при выгрузке файла. Проверьте логи.");
            });
        }, "Формирование файла Excel...");
    }

    [RelayCommand]
    private async Task BatchUpdateFromRegistryAsync()
    {
        var msgResult = MessageBox.Show(
            "Запустить автоматическое обогащение данных для всех контрагентов с заполненным ИНН?\nПроцесс может занять несколько минут в зависимости от количества записей.",
            "Массовая синхронизация с ФНС",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (msgResult != MessageBoxResult.Yes) return;

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            var targetCustomers = await context.Customers
                .Where(c => !string.IsNullOrEmpty(c.INN))
                .ToListAsync();

            int totalCount = targetCustomers.Count;

            if (totalCount == 0)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Info("Не найдено контрагентов с заполненным ИНН."));
                return;
            }

            int updatedCount = 0;
            int notFoundCount = 0;
            int errorCount = 0;
            int processedCount = 0;

            var syncData = new ConcurrentBag<(Customer Entity, DaDataSuggestion Suggestion, (double Lat, double Lon)? Coords)>();
            using var semaphore = new SemaphoreSlim(5);

            var tasks = targetCustomers.Select(async customer =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var suggestion = await _daDataService.GetPartyByInnAsync(customer.INN!);

                    if (suggestion != null && suggestion.Data != null)
                    {
                        var rawAddress = suggestion.Data.Address?.Value ?? string.Empty;
                        var formattedAddress = FormatAddressString(rawAddress);
                        (double Lat, double Lon)? point = null;

                        if (!string.IsNullOrWhiteSpace(formattedAddress))
                        {
                            var coords = await GetCoordinatesWithFallbackAsync(formattedAddress);
                            if (coords.HasValue) point = coords;
                        }

                        syncData.Add((customer, suggestion, point));
                    }
                    else
                    {
                        Interlocked.Increment(ref notFoundCount);
                    }

                    await Task.Delay(200);
                }
                catch
                {
                    Interlocked.Increment(ref errorCount);
                }
                finally
                {
                    int current = Interlocked.Increment(ref processedCount);
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _overlay.UpdateMessage($"Синхронизация ФНС: обработано {current} из {totalCount}...");
                    });

                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            foreach (var item in syncData)
            {
                var c = item.Entity;
                var s = item.Suggestion.Data!;

                c.Type = s.Type == "INDIVIDUAL" ? CustomerType.Entrepreneur : CustomerType.LegalEntity;
                c.Name = s.Name?.ShortWithOpf ?? item.Suggestion.Value ?? c.Name;
                c.FullName = s.Name?.FullWithOpf ?? item.Suggestion.Value ?? c.FullName;
                c.KPP = s.Kpp ?? c.KPP;
                c.OGRN = s.Ogrn ?? c.OGRN;

                string newAddress = FormatAddressString(s.Address?.Value ?? string.Empty);
                c.LegalAddress = newAddress;
                c.Address = newAddress;
                c.ContactPerson = s.Management?.Name ?? c.ContactPerson;

                if (item.Coords.HasValue)
                {
                    c.GeoLat = item.Coords.Value.Lat;
                    c.GeoLon = item.Coords.Value.Lon;
                }

                updatedCount++;
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                _ = LoadDataAsync();
                _notify.Success($"Синхронизация завершена.\nОбновлено: {updatedCount}\nНе найдено в ФНС: {notFoundCount}\nОшибок: {errorCount}");
            });

        }, "Инициализация синхронизации...");
    }

    private async Task<(double Latitude, double Longitude)?> GetCoordinatesWithFallbackAsync(string targetAddress)
    {
        if (string.IsNullOrWhiteSpace(targetAddress)) return null;

        var coords = await _geoService.GetCoordinatesAsync(targetAddress);
        if (coords.HasValue) return coords;

        var parts = targetAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .ToList();

        if (parts.Count <= 1)
        {
            parts = targetAddress.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .ToList();
        }

        while (parts.Count > 1)
        {
            parts.RemoveAt(parts.Count - 1);
            string fallbackAddress = string.Join(" ", parts);
            coords = await _geoService.GetCoordinatesAsync(fallbackAddress);

            if (coords.HasValue) return coords;
        }

        return null;
    }

    private string FormatAddressString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string result = Regex.Replace(input, @"\b(г|ул|д|кв|стр|корп|пер|пр-кт|обл|р-н|пос|с|к)\.\s*", "$1 ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s+", " ").Trim();
        result = Regex.Replace(result, @"\s+,", ",");
        result = Regex.Replace(result, @",([^\s])", ", $1");
        return result;
    }
}
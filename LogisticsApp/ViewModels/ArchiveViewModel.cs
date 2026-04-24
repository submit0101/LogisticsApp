using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.ViewModels;

public class ArchiveItemDto
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
    public string DeletedAtStr => DeletedAt?.ToString("dd.MM.yyyy HH:mm") ?? "";
}

public partial class ArchiveViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private ObservableCollection<ArchiveItemDto> _archiveItems = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceDeleteCommand))]
    private ArchiveItemDto? _selectedItem;

    public ArchiveViewModel(IDbContextFactory<LogisticsDbContext> dbFactory, NotificationService notify, IDialogService dialogService)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _dialogService = dialogService;

        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var items = new System.Collections.Generic.List<ArchiveItemDto>();

            var customers = await context.Customers.IgnoreQueryFilters().Where(c => c.IsDeleted).ToListAsync();
            items.AddRange(customers.Select(c => new ArchiveItemDto { EntityType = "Контрагент", EntityId = c.CustomerID, Description = $"{c.INN} - {c.Name}", DeletedAt = c.DeletedAt }));

            var drivers = await context.Drivers.IgnoreQueryFilters().Where(d => d.IsDeleted).ToListAsync();
            items.AddRange(drivers.Select(d => new ArchiveItemDto { EntityType = "Водитель", EntityId = d.DriverID, Description = d.FullName, DeletedAt = d.DeletedAt }));

            var vehicles = await context.Vehicles.IgnoreQueryFilters().Where(v => v.IsDeleted).ToListAsync();
            items.AddRange(vehicles.Select(v => new ArchiveItemDto { EntityType = "Автомобиль", EntityId = v.VehicleID, Description = $"{v.RegNumber} ({v.Model})", DeletedAt = v.DeletedAt }));

            var orders = await context.Orders.IgnoreQueryFilters().Include(o => o.Customer).Where(o => o.IsDeleted).ToListAsync();
            items.AddRange(orders.Select(o => new ArchiveItemDto { EntityType = "Заказ", EntityId = o.OrderID, Description = $"Заказ №{o.OrderID} от {o.Customer?.Name}", DeletedAt = o.DeletedAt }));

            var products = await context.Products.IgnoreQueryFilters().Where(p => p.IsDeleted).ToListAsync();
            items.AddRange(products.Select(p => new ArchiveItemDto { EntityType = "Номенклатура", EntityId = p.ProductID, Description = $"{p.SKU} - {p.Name}", DeletedAt = p.DeletedAt }));

            var waybills = await context.Waybills.IgnoreQueryFilters().Where(w => w.IsDeleted).ToListAsync();
            items.AddRange(waybills.Select(w => new ArchiveItemDto { EntityType = "Путевой лист", EntityId = w.WaybillID, Description = $"П/Л №{w.WaybillID}", DeletedAt = w.DeletedAt }));

            Application.Current.Dispatcher.Invoke(() =>
            {
                ArchiveItems.Clear();
                foreach (var item in items.OrderByDescending(i => i.DeletedAt)) ArchiveItems.Add(item);
            });
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка загрузки архива: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RestoreAsync()
    {
        if (SelectedItem == null) return;

        if (_dialogService.ShowConfirmation("Восстановление", $"Восстановить {SelectedItem.EntityType} '{SelectedItem.Description}' из архива?"))
        {
            IsLoading = true;
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();
                ISoftDeletable? entity = null;

                switch (SelectedItem.EntityType)
                {
                    case "Контрагент": entity = await context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.CustomerID == SelectedItem.EntityId); break;
                    case "Водитель": entity = await context.Drivers.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.DriverID == SelectedItem.EntityId); break;
                    case "Автомобиль": entity = await context.Vehicles.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.VehicleID == SelectedItem.EntityId); break;
                    case "Заказ": entity = await context.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.OrderID == SelectedItem.EntityId); break;
                    case "Номенклатура": entity = await context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.ProductID == SelectedItem.EntityId); break;
                    case "Путевой лист": entity = await context.Waybills.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.WaybillID == SelectedItem.EntityId); break;
                }

                if (entity != null)
                {
                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                    await context.SaveChangesAsync();

                    _notify.Success("Успешно восстановлено!");
                    await LoadDataAsync();
                }
            }
            catch (Exception ex) { _notify.Error(ex.Message); }
            finally { IsLoading = false; }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ForceDeleteAsync()
    {
        if (SelectedItem == null) return;

        if (_dialogService.ShowConfirmation("Удаление навсегда", $"ВНИМАНИЕ! Физически удалить {SelectedItem.EntityType} '{SelectedItem.Description}' без возможности восстановления?"))
        {
            IsLoading = true;
            try
            {
                using var context = await _dbFactory.CreateDbContextAsync();

                // Используем ExecuteDeleteAsync для физического удаления в обход трекера
                switch (SelectedItem.EntityType)
                {
                    case "Контрагент":
                        await context.Customers.IgnoreQueryFilters().Where(e => e.CustomerID == SelectedItem.EntityId).ExecuteDeleteAsync();
                        break;
                    case "Водитель":
                        await context.Drivers.IgnoreQueryFilters().Where(e => e.DriverID == SelectedItem.EntityId).ExecuteDeleteAsync();
                        break;
                    case "Автомобиль":
                        await context.Vehicles.IgnoreQueryFilters().Where(e => e.VehicleID == SelectedItem.EntityId).ExecuteDeleteAsync();
                        break;
                    case "Заказ":
                        await context.Orders.IgnoreQueryFilters().Where(e => e.OrderID == SelectedItem.EntityId).ExecuteDeleteAsync();
                        break;
                    case "Номенклатура":
                        await context.Products.IgnoreQueryFilters().Where(e => e.ProductID == SelectedItem.EntityId).ExecuteDeleteAsync();
                        break;
                    case "Путевой лист":
                        await context.Waybills.IgnoreQueryFilters().Where(e => e.WaybillID == SelectedItem.EntityId).ExecuteDeleteAsync();
                        break;
                }

                _notify.Success("Удалено навсегда!");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _notify.Error($"Невозможно удалить, есть связанные данные: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private bool HasSelection() => SelectedItem != null;
}
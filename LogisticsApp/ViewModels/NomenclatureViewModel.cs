using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.DTOs;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace LogisticsApp.ViewModels;

public partial class NomenclatureViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly NotificationService _notify;
    private readonly ExcelImportService _importService;
    private readonly ExcelExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly SecurityService _security;
    private readonly object _lockObj = new();

    private List<ProductListDto> _allProducts = new();
    private List<ProductGroup> _allGroups = new();

    public ObservableCollection<NomenclatureNode> GroupNodes { get; } = new();
    public ObservableCollection<ProductListDto> Products { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = string.Empty;

    private NomenclatureNode? _selectedNode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditProductCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProductCommand))]
    private ProductListDto? _selectedProduct;

    public NomenclatureViewModel(
        IDbContextFactory<LogisticsDbContext> dbFactory,
        NotificationService notify,
        ExcelImportService importService,
        ExcelExportService exportService,
        IDialogService dialogService,
        SecurityService security)
    {
        _dbFactory = dbFactory;
        _notify = notify;
        _importService = importService;
        _exportService = exportService;
        _dialogService = dialogService;
        _security = security;

        BindingOperations.EnableCollectionSynchronization(Products, _lockObj);
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ViewNomenclature)) return;

        IsLoading = true;
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();

            _allGroups = await context.ProductGroups.AsNoTracking().ToListAsync();

            var dbProducts = await context.Products.AsNoTracking().Include(p => p.Group).Include(p => p.Prices).OrderBy(p => p.Name).ToListAsync();
            var today = DateTime.Now;

            _allProducts = dbProducts.Select(p => new ProductListDto
            {
                ProductID = p.ProductID,
                GroupID = p.GroupID,
                GroupName = p.Group?.Name ?? string.Empty,
                SKU = p.SKU,
                Name = p.Name,
                ShelfLife = p.ShelfLife ?? string.Empty,
                StorageConditions = p.StorageConditions ?? string.Empty,
                Barcode = p.Barcode ?? string.Empty,
                CurrentPrice = p.Prices
                    .Where(price => price.Period <= today)
                    .OrderByDescending(price => price.Period)
                    .Select(price => price.Value)
                    .FirstOrDefault()
            }).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                BuildTree();
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => _notify.Error(ex.Message));
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() => IsLoading = false);
        }
    }

    private void BuildTree()
    {
        GroupNodes.Clear();

        var rootNode = new NomenclatureNode(this)
        {
            Name = "Вся номенклатура",
            IsRoot = true,
            IsExpanded = true
        };

        var lookup = new Dictionary<int, NomenclatureNode>();
        foreach (var g in _allGroups.OrderBy(g => g.Name))
        {
            lookup[g.GroupID] = new NomenclatureNode(this) { Name = g.Name, Group = g };
        }

        foreach (var g in _allGroups)
        {
            var node = lookup[g.GroupID];
            if (g.ParentGroupID.HasValue && lookup.TryGetValue(g.ParentGroupID.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                rootNode.Children.Add(node);
            }
        }

        GroupNodes.Add(rootNode);

        if (_selectedNode == null)
        {
            rootNode.IsSelected = true;
        }
        else
        {
            var nodeToSelect = FindNode(rootNode, _selectedNode.Group?.GroupID);
            if (nodeToSelect != null) nodeToSelect.IsSelected = true;
            else rootNode.IsSelected = true;
        }
    }

    private NomenclatureNode? FindNode(NomenclatureNode node, int? groupId)
    {
        if (node.Group?.GroupID == groupId) return node;
        foreach (var child in node.Children)
        {
            var found = FindNode(child, groupId);
            if (found != null) return found;
        }
        return null;
    }

    public void SelectNode(NomenclatureNode node)
    {
        _selectedNode = node;
        ApplyFilter();
    }

    private HashSet<int> GetGroupAndSubgroupIds(NomenclatureNode node)
    {
        var ids = new HashSet<int>();
        if (node.Group != null)
        {
            ids.Add(node.Group.GroupID);
        }
        foreach (var child in node.Children)
        {
            ids.UnionWith(GetGroupAndSubgroupIds(child));
        }
        return ids;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(p => p.Name.ToLower().Contains(search) || p.SKU.ToLower().Contains(search));
        }

        if (_selectedNode != null && !_selectedNode.IsRoot)
        {
            var groupIds = GetGroupAndSubgroupIds(_selectedNode);
            filtered = filtered.Where(p => p.GroupID.HasValue && groupIds.Contains(p.GroupID.Value));
        }

        lock (_lockObj)
        {
            Products.Clear();
            foreach (var product in filtered)
            {
                Products.Add(product);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddProduct))]
    private void AddProduct()
    {
        if (_dialogService.ShowProductEditor(null))
        {
            _ = LoadDataAsync();
        }
    }
    private bool CanAddProduct() => _security.HasPermission(AppPermission.EditNomenclature);

    [RelayCommand(CanExecute = nameof(CanEditProduct))]
    private void EditProduct()
    {
        if (SelectedProduct != null && _dialogService.ShowProductEditor(SelectedProduct.ProductID))
        {
            _ = LoadDataAsync();
        }
    }
    private bool CanEditProduct() => SelectedProduct != null && _security.HasPermission(AppPermission.EditNomenclature);

    [RelayCommand(CanExecute = nameof(CanDeleteProduct))]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct == null) return;
        if (!_dialogService.ShowConfirmation("Подтверждение", $"Удалить товар '{SelectedProduct.Name}'?")) return;

        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var product = await context.Products.FindAsync(SelectedProduct.ProductID);

            if (product != null)
            {
                bool hasOrders = await context.OrderItems.AnyAsync(oi => oi.ProductID == product.ProductID);
                if (hasOrders)
                {
                    _notify.Warning("Нельзя удалить товар, который уже используется в заказах.");
                    return;
                }

                context.Products.Remove(product);
                await context.SaveChangesAsync();
                _notify.Success("Товар удален");
                await LoadDataAsync();
            }
        }
        catch (Exception ex) { _notify.Error(ex.Message); }
    }
    private bool CanDeleteProduct() => SelectedProduct != null && _security.HasPermission(AppPermission.DeleteNomenclature);

    [RelayCommand(CanExecute = nameof(CanAddProduct))]
    private async Task ImportAsync()
    {
        var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls" };
        if (ofd.ShowDialog() == true)
        {
            IsLoading = true;
            try
            {
                var result = await _importService.ImportNomenclatureAsync(ofd.FileName);
                if (result.Errors > 0)
                    _notify.Warning($"Загружено: {result.Added}, Обновлено: {result.Updated}, Ошибок: {result.Errors}");
                else
                    _notify.Success($"Импорт завершен. Загружено: {result.Added}, Обновлено: {result.Updated}");

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _notify.Error($"Ошибка импорта: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void Export()
    {
        if (!_security.HasPermission(AppPermission.ViewNomenclature)) return;

        if (!_allProducts.Any())
        {
            _notify.Warning("Нет данных для экспорта!");
            return;
        }

        IEnumerable<ProductListDto> dataToExport = _allProducts;

        if (_selectedNode != null && !_selectedNode.IsRoot)
        {
            bool exportOnlyGroup = _dialogService.ShowConfirmation(
                "Настройка выгрузки",
                $"У вас выбрана товарная группа '{_selectedNode.Name}'.\n\nНажмите 'Да', чтобы выгрузить только эту группу и её подпапки.\nНажмите 'Нет', чтобы выгрузить весь прайс-лист целиком."
            );

            if (exportOnlyGroup)
            {
                var groupIds = GetGroupAndSubgroupIds(_selectedNode);
                dataToExport = _allProducts.Where(p => p.GroupID.HasValue && groupIds.Contains(p.GroupID.Value)).ToList();

                if (!dataToExport.Any())
                {
                    _notify.Warning("В выбранной группе и её подпапках нет товаров!");
                    return;
                }
            }
        }

        _exportService.ExportNomenclaturePriceList(dataToExport, "Прайс_Лист_БМК");
    }
}
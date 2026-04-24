using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LogisticsApp.Models;

namespace LogisticsApp.ViewModels;

/// <summary>
/// Представляет узел (папку) в дереве номенклатуры
/// </summary>
public partial class NomenclatureNode : ObservableObject
{
    private readonly NomenclatureViewModel _parentVm;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ProductGroup? _group;
    [ObservableProperty] private bool _isRoot;
    [ObservableProperty] private bool _isExpanded;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                _parentVm.SelectNode(this);
            }
        }
    }

    public ObservableCollection<NomenclatureNode> Children { get; } = new();

    public NomenclatureNode(NomenclatureViewModel parentVm)
    {
        _parentVm = parentVm;
    }
}
using System.Windows.Controls;
using LogisticsApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Views;

public partial class InventoryPage : UserControl
{
    public InventoryPage()
    {
        InitializeComponent();
        var viewModel = App.AppHost!.Services.GetRequiredService<InventoryViewModel>();
        DataContext = viewModel;
        this.Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
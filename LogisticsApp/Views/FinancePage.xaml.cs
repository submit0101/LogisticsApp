using System.Windows.Controls;
using LogisticsApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Views;

public partial class FinancePage : UserControl
{
    public FinancePage()
    {
        InitializeComponent();
        var viewModel = App.AppHost!.Services.GetRequiredService<FinanceViewModel>();
        DataContext = viewModel;
        this.Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
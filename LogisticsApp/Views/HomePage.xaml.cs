using System.Windows.Controls;
using LogisticsApp.ViewModels;

namespace LogisticsApp.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
        DataContext = new HomeViewModel();
    }
}
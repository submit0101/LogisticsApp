using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Models;

namespace LogisticsApp.ViewModels.Windows;

public partial class HelpViewModel : ViewModelBase
{
    [ObservableProperty] private HelpDocument _document = new();

    public void Initialize(HelpDocument document)
    {
        Document = document;
    }

    [RelayCommand]
    private void Close(System.Windows.Window window)
    {
        window?.Close();
    }
}
using System.Windows;
using System.Windows.Input;
using LogisticsApp.ViewModels.Windows;
using MahApps.Metro.Controls;

namespace LogisticsApp.Views.Windows;

public partial class LoginWindow : MetroWindow
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadSavedCredentials(TxtPassword);

        if (string.IsNullOrEmpty(_viewModel.Username))
        {
            var request = new TraversalRequest(FocusNavigationDirection.Next);
            request.Wrapped = true;
            MoveFocus(request);
        }
        else
        {
            TxtPassword.Focus();
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }
}
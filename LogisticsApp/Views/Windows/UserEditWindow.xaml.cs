using System.Windows;
using System.Windows.Controls;
using LogisticsApp.ViewModels.Windows;

namespace LogisticsApp.Views.Windows;

public partial class UserEditWindow
{
    public UserEditWindow()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserEditorViewModel vm && sender is PasswordBox pb)
        {
            vm.PlainPassword = pb.Password;
        }
    }
}
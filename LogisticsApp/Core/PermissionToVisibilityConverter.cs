using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LogisticsApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Core;

public class PermissionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string permissionName && Enum.TryParse(permissionName, out AppPermission permission))
        {
            var security = App.AppHost!.Services.GetRequiredService<SecurityService>();
            return security.HasPermission(permission) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
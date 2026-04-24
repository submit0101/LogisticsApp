using System;
using System.Globalization;
using System.Windows.Data;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Views.Windows;

public partial class DriverEditWindow
{
    public DriverEditWindow()
    {
        InitializeComponent();
    }
}

public class DriverInverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue) return !boolValue;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue) return !boolValue;
        return true;
    }
}

public class DriverStatusToTextConverterLocal : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DriverStatus status)
        {
            return status switch
            {
                DriverStatus.Active => "Активен",
                DriverStatus.OnLeave => "В отпуске",
                DriverStatus.SickLeave => "На больничном",
                DriverStatus.Dismissed => "Уволен",
                _ => status.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
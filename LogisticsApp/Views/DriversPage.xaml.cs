using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LogisticsApp.Models.Enums;
using LogisticsApp.ViewModels;

namespace LogisticsApp.Views;

public partial class DriversPage : UserControl
{
    public DriversPage()
    {
        InitializeComponent();
    }
}

public class DriverStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DriverStatus status)
        {
            return status switch
            {
                DriverStatus.Active => new SolidColorBrush(Color.FromRgb(76, 175, 80)),      
                DriverStatus.OnLeave => new SolidColorBrush(Color.FromRgb(255, 152, 0)),     
                DriverStatus.SickLeave => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   
                DriverStatus.Dismissed => new SolidColorBrush(Color.FromRgb(158, 158, 158)), 
                _ => Brushes.Gray
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class DriverStatusToTextConverter : IValueConverter
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

public class DriverExpiryWarningConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3) return Visibility.Collapsed;

        var licenseExp = values[0] as DateTime?;
        var medCertExp = values[1] as DateTime?;
        var warningDays = values[2] as int? ?? 14;

        var threshold = DateTime.Today.AddDays(warningDays);

        if ((licenseExp.HasValue && licenseExp.Value.Date <= threshold) ||
            (medCertExp.HasValue && medCertExp.Value.Date <= threshold))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
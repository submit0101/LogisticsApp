using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LogisticsApp.Models.Enums;
using MahApps.Metro.Controls;

namespace LogisticsApp.Views.Windows;

public partial class WaybillEditWindow : MetroWindow
{
    public WaybillEditWindow()
    {
        InitializeComponent();
    }
}

public class WaybillInverseBooleanConverter : IValueConverter
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

public class WaybillInverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue) return boolValue ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class WaybillPointStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaybillPointStatus status)
        {
            return status switch
            {
                WaybillPointStatus.Pending => "Ожидание",
                WaybillPointStatus.Delivered => "Доставлено (Полностью)",
                WaybillPointStatus.Failed => "Отказ / Срыв",
                WaybillPointStatus.PartiallyDelivered => "Доставлено (Частично)",
                _ => status.ToString()
            };
        }
        return string.Empty;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class WaybillPointStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaybillPointStatus status)
        {
            return status switch
            {
                WaybillPointStatus.Pending => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                WaybillPointStatus.Delivered => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                WaybillPointStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                WaybillPointStatus.PartiallyDelivered => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                _ => Brushes.Black
            };
        }
        return Brushes.Black;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PartialDeliveryVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaybillPointStatus status && status == WaybillPointStatus.PartiallyDelivered)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
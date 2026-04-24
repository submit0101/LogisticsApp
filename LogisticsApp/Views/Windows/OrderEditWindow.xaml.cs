using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LogisticsApp.Models.Enums;
using MahApps.Metro.Controls;

namespace LogisticsApp.Views.Windows;

public partial class OrderEditWindow : MetroWindow
{
    public OrderEditWindow()
    {
        InitializeComponent();
    }
}

public class OrderInverseBooleanConverter : IValueConverter
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

public class OrderPriorityToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is OrderPriority priority)
        {
            return priority switch
            {
                OrderPriority.Low => "Низкий",
                OrderPriority.Normal => "Обычный",
                OrderPriority.High => "Высокий",
                OrderPriority.Critical => "КРИТИЧЕСКИЙ (VIP)",
                _ => priority.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
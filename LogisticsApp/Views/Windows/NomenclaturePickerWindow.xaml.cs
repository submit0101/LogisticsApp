using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.Controls;

namespace LogisticsApp.Views.Windows;

public partial class NomenclaturePickerWindow : MetroWindow
{
    public NomenclaturePickerWindow()
    {
        InitializeComponent();
    }
}

public class NomenclatureInverseBooleanConverter : IValueConverter
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

public class NumericGreaterThanZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue) return intValue > 0;
        if (value is double doubleValue) return doubleValue > 0;
        if (value is decimal decimalValue) return decimalValue > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
using System;
using System.Globalization;
using System.Windows.Data;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Views.Windows;

public partial class VehicleEditWindow
{
    public VehicleEditWindow()
    {
        InitializeComponent();
    }
}

public class VehicleInverseBooleanConverter : IValueConverter
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

public class VehicleServiceTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VehicleServiceType type)
        {
            return type switch
            {
                VehicleServiceType.RoutineMaintenance => "Регулярное ТО",
                VehicleServiceType.Repair => "Ремонт",
                VehicleServiceType.Inspection => "Осмотр / Диагностика",
                VehicleServiceType.TireChange => "Шиномонтаж",
                VehicleServiceType.Sanitization => "Санобработка",
                _ => type.ToString()
            };
        }
        return string.Empty;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogisticsApp.Core;

public class OrderStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string status = value as string ?? "Draft";
        return status switch
        {
            "Draft" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),    // Gray
            "New" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),       // Orange
            "Planned" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),  // Blue
            "InTransit" => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
            "Delivered" => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
            "Canceled" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red
            "Cancelled" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
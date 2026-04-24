using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Core;

public class WaybillStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaybillStatus status)
        {
            return status switch
            {
                WaybillStatus.Draft => new SolidColorBrush(Color.FromRgb(158, 158, 158)),    
                WaybillStatus.Planned => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   
                WaybillStatus.Active => new SolidColorBrush(Color.FromRgb(59, 130, 246)),    
                WaybillStatus.Completed => new SolidColorBrush(Color.FromRgb(16, 185, 129)), 
                WaybillStatus.Cancelled => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
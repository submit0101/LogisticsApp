using System;
using System.Globalization;
using System.Windows.Data;

namespace LogisticsApp.Core;

public class OrderStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Draft" => "В подготовке",
                "New" => "Новый (К распределению)",
                "Planned" => "В плане (Назначен)",
                "InTransit" => "В пути (Доставляется)",
                "Delivered" => "Доставлен",
                "Canceled" => "Отменен",
                "Cancelled" => "Отменен",
                _ => status
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
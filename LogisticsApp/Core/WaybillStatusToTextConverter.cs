using System;
using System.Globalization;
using System.Windows.Data;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Core;

public class WaybillStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WaybillStatus status)
        {
            return status switch
            {
                WaybillStatus.Draft => "В подготовке",
                WaybillStatus.Planned => "К выполнению",
                WaybillStatus.Active => "В рейсе",
                WaybillStatus.Completed => "Завершен",
                WaybillStatus.Cancelled => "Отменен",
                _ => status.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
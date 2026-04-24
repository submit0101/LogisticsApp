using System;
using System.Globalization;
using System.Windows.Data;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Core;

public class FuelTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FuelType type)
        {
            return type switch
            {
                FuelType.AI92 => "АИ-92",
                FuelType.AI95 => "АИ-95",
                FuelType.AI98 => "АИ-98",
                FuelType.DT => "ДТ (Дизель)",
                FuelType.GasPropan => "Газ (Пропан)",
                FuelType.GasMetan => "Газ (Метан)",
                _ => type.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
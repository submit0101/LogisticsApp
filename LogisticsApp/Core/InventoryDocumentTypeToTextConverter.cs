using System;
using System.Globalization;
using System.Windows.Data;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Core;

public class InventoryDocumentTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is InventoryDocumentType type)
        {
            return type switch
            {
                InventoryDocumentType.Receipt => "Оприходование",
                InventoryDocumentType.WriteOff => "Списание",
                InventoryDocumentType.Correction => "Корректировка",
                InventoryDocumentType.Request => "Заявка (Дефицит)",
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
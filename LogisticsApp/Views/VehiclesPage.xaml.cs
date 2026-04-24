using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LogisticsApp.Models.Enums;

namespace LogisticsApp.Views;

public partial class VehiclesPage : UserControl
{
    public VehiclesPage()
    {
        InitializeComponent();
    }
}

public class VehicleStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VehicleStatus status)
        {
            return status switch
            {
                VehicleStatus.Active => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                VehicleStatus.InService => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                VehicleStatus.Inactive => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                VehicleStatus.Decommissioned => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class VehicleStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VehicleStatus status)
        {
            return status switch
            {
                VehicleStatus.Active => "На линии",
                VehicleStatus.InService => "В ремонте / ТО",
                VehicleStatus.Inactive => "В резерве",
                VehicleStatus.Decommissioned => "Списан",
                _ => status.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
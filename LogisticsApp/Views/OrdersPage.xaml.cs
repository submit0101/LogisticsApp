using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace LogisticsApp.Views;

public partial class OrdersPage : UserControl
{
    public OrdersPage()
    {
        InitializeComponent();
    }
}

public class OrderStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string status = value as string ?? "New";
        return status switch
        {
            "New" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),       
            "Planned" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),  
            "Delivered" => new SolidColorBrush(Color.FromRgb(76, 175, 80)), 
            "Canceled" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            _ => Brushes.Gray
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class OrderStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string status = value as string ?? "";
        return status switch
        {
            "New" => "Новый",
            "Planned" => "В плане",
            "Delivered" => "Доставлен",
            "Canceled" => "Отменен",
            _ => status
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
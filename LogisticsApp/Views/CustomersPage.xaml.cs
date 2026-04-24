using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LogisticsApp.Models;

namespace LogisticsApp.Views;

public partial class CustomersPage : UserControl
{
    public CustomersPage()
    {
        InitializeComponent();
    }
}

public class CustomerTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CustomerType type)
        {
            return type switch
            {
                CustomerType.LegalEntity => "Юр. лицо",
                CustomerType.Entrepreneur => "ИП",
                CustomerType.PhysicalPerson => "Физ. лицо",
                _ => "Неизвестно"
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ContactWarningConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3) return Visibility.Visible;

        string phone = values[0] as string ?? string.Empty;
        string email = values[1] as string ?? string.Empty;
        string contactPerson = values[2] as string ?? string.Empty;

        bool isPhoneValid = !string.IsNullOrWhiteSpace(phone) && phone.Length >= 5;

        bool isEmailValid = !string.IsNullOrWhiteSpace(email) && email.Contains("@") && email.Contains(".");

        bool isContactPersonValid = !string.IsNullOrWhiteSpace(contactPerson);


        if ((!isPhoneValid && !isEmailValid) || !isContactPersonValid)
        {
            return Visibility.Visible; 
        }

        return Visibility.Collapsed; 
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using LogisticsApp.Models;
using LogisticsApp.Services;
using LogisticsApp.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using MahApps.Metro.Controls;

namespace LogisticsApp.Views.Windows;

public partial class CustomerEditWindow : MetroWindow
{
    private bool _isMapReady = false;
    private readonly ConcurrentQueue<string> _pendingScripts = new();

    public CustomerEditWindow(ISettingsService settingsService)
    {
        InitializeComponent();
        InitializeMapAsync();
        this.Loaded += CustomerEditWindow_Loaded;
        this.Closed += CustomerEditWindow_Closed;
    }

    private async void InitializeMapAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "LogisticsApp_WebView"));
            await MapWebView.EnsureCoreWebView2Async(env);
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "yandex_map.html");
            MapWebView.Source = new Uri(htmlPath);
            MapWebView.WebMessageReceived += MapWebView_WebMessageReceived;
            MapWebView.NavigationCompleted += MapWebView_NavigationCompleted;
        }
        catch (Exception ex)
        {
            var notify = App.AppHost!.Services.GetRequiredService<NotificationService>();
            notify.Warning($"Не удалось инициализировать карту: {ex.Message}");
        }
    }

    private void CustomerEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomerEditorViewModel vm)
        {
            // Подписываемся на событие ViewModel для отрисовки маркера
            vm.OnMapLocationChanged += ViewModel_OnMapLocationChanged;
        }
    }

    private void ViewModel_OnMapLocationChanged(double lat, double lon, string title)
    {
        string jsTitle = string.IsNullOrWhiteSpace(title) ? "Точка доставки" : title.Replace("'", "\\'");
        string script = $@"
            clearMap();
            addMarker({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{jsTitle}', 'Red');
            setCenter({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 16);
        ";

        ExecuteOrQueueScript(script);
    }

    private void ExecuteOrQueueScript(string script)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_isMapReady && MapWebView?.CoreWebView2 != null)
            {
                MapWebView.ExecuteScriptAsync(script);
            }
            else
            {
                _pendingScripts.Enqueue(script);
            }
        });
    }

    private void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _isMapReady = true;

            // Выполняем все скрипты, которые скопились до загрузки карты (решает Race Condition)
            while (_pendingScripts.TryDequeue(out var script))
            {
                MapWebView.ExecuteScriptAsync(script);
            }

            // Отрисовываем текущие координаты при загрузке, если они уже есть
            if (DataContext is CustomerEditorViewModel vm)
            {
                if (vm.GeoLat.HasValue && vm.GeoLon.HasValue)
                {
                    ViewModel_OnMapLocationChanged(vm.GeoLat.Value, vm.GeoLon.Value, vm.Name ?? "Точка доставки");
                }
            }
        }
    }

    private void MapWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(message)) return;

        try
        {
            using var doc = JsonDocument.Parse(message);
            if (doc.RootElement.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "dblclick")
            {
                if (DataContext is CustomerEditorViewModel vm && vm.UpdateCoordinatesFromMapCommand.CanExecute(message))
                {
                    // Вызываем новую безопасную команду из ViewModel (не стирающую адрес)
                    vm.UpdateCoordinatesFromMapCommand.Execute(message);
                }
            }
        }
        catch { }
    }

    private void CustomerEditWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is CustomerEditorViewModel vm)
        {
            vm.OnMapLocationChanged -= ViewModel_OnMapLocationChanged;
        }

        if (MapWebView != null)
        {
            MapWebView.WebMessageReceived -= MapWebView_WebMessageReceived;
            MapWebView.NavigationCompleted -= MapWebView_NavigationCompleted;
            MapWebView.Dispose();
        }
    }
}

public class CustomerInverseBooleanConverter : IValueConverter
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
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using LogisticsApp.Models;
using LogisticsApp.Services;
using LogisticsApp.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using MahApps.Metro.Controls;

namespace LogisticsApp.Views;

public partial class RouteMapWindow : MetroWindow
{
    private readonly RouteMapViewModel _viewModel;
    private bool _isMapReady = false;

    public RouteMapWindow(Waybill waybill)
    {
        InitializeComponent();

        var geoService = App.AppHost!.Services.GetRequiredService<GeocodingService>();
        var notifyService = App.AppHost!.Services.GetRequiredService<NotificationService>();

        _viewModel = new RouteMapViewModel(waybill, geoService, notifyService);
        DataContext = _viewModel;

        _viewModel.OnExecuteScript += (script) =>
        {
            if (_isMapReady && MapWebView != null && MapWebView.CoreWebView2 != null)
            {
                Dispatcher.Invoke(() => MapWebView.ExecuteScriptAsync(script));
            }
        };

        MapWebView.SizeChanged += (s, e) =>
        {
            if (_isMapReady && MapWebView != null && MapWebView.CoreWebView2 != null)
            {
                Dispatcher.Invoke(() => MapWebView.ExecuteScriptAsync("if(typeof myMap !== 'undefined') myMap.container.fitToViewport();"));
            }
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
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

    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _isMapReady = true;
            _viewModel.DrawGeofence();
            await _viewModel.DrawRouteAsync();
        }
    }

    private void MapWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (!string.IsNullOrEmpty(msg))
        {
            try
            {
                using var doc = JsonDocument.Parse(msg);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    string type = typeProp.GetString() ?? string.Empty;
                    if (type == "routeMetrics" || type == "routeInteractiveUpdate")
                    {
                        double distance = root.GetProperty("distance").GetDouble();
                        double time = root.GetProperty("time").GetDouble();
                        double timeInTraffic = root.TryGetProperty("timeInTraffic", out var trafficProp) ? trafficProp.GetDouble() : time;
                        _viewModel.UpdateMetricsFromMap(distance, time, timeInTraffic);
                    }
                }
            }
            catch { }
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (MapWebView != null)
        {
            MapWebView.WebMessageReceived -= MapWebView_WebMessageReceived;
            MapWebView.NavigationCompleted -= MapWebView_NavigationCompleted;
            MapWebView.Dispose();
        }
    }
}
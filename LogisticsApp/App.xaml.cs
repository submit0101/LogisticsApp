using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using ControlzEx.Theming;
using LogisticsApp.Data;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using LogisticsApp.Services.Implementations;
using LogisticsApp.ViewModels;
using LogisticsApp.ViewModels.Windows;
using LogisticsApp.Views;
using LogisticsApp.Views.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using QuestPDF.Infrastructure;
using Serilog;

namespace LogisticsApp;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IDatabaseManagementService, DatabaseManagementService>();
                services.AddSingleton<ILogReaderService, LogReaderService>();
                services.AddSingleton<IReportDataService, ReportDataService>();
                services.AddSingleton<SecurityService>();
                services.AddSingleton<NotificationService>();
                services.AddSingleton<OverlayService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<AuditLogChannel>();
                services.AddSingleton<AuditInterceptor>();
                services.AddSingleton<InventoryService>();
                services.AddSingleton<FuelPriceService>();
                services.AddSingleton<TripValidationService>();
                services.AddSingleton<WaybillDispatchService>();
                services.AddSingleton<TripValidationService>();
                services.AddSingleton<WaybillDispatchService>();
                services.AddSingleton<RouteCalculationService>();
                services.AddHostedService<AuditLogBackgroundService>();

                services.AddHostedService<AuditLogBackgroundService>();
                services.AddHostedService<ArchiveCleanupBackgroundService>();

                var sp = services.BuildServiceProvider();
                var settings = sp.GetRequiredService<ISettingsService>().Current;

                string connString = string.IsNullOrWhiteSpace(settings.ConnectionString)
                    ? @"Server=(localdb)\mssqllocaldb;Database=LogisticsAppDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : settings.ConnectionString;

                services.AddDbContextFactory<LogisticsDbContext>((provider, options) =>
                {
                    options.UseSqlServer(connString)
                           .AddInterceptors(provider.GetRequiredService<AuditInterceptor>());
                });

                var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

                services.AddHttpClient<DaDataService>().AddPolicyHandler(retryPolicy);
                services.AddHttpClient<GeocodingService>().AddPolicyHandler(retryPolicy);
                services.AddHttpClient("FuelApiClient").AddPolicyHandler(retryPolicy);

                services.AddTransient<IAuthService, AuthService>();

                services.AddTransient<IWaybillDocumentService, WaybillDocumentService>();
                services.AddTransient<IOrderReportService, OrderReportService>();
                services.AddTransient<ILogisticsAnalyticsReportService, LogisticsAnalyticsReportService>();
                services.AddTransient<IInventoryReportService, InventoryReportService>();
                services.AddTransient<IFinanceReportService, FinanceReportService>();
                services.AddTransient<ExcelExportService>();
                services.AddTransient<ExcelImportService>();
                services.AddSingleton<IHelpService, HelpService>();

                services.AddSingleton(provider =>
                {
                    var security = provider.GetRequiredService<SecurityService>();
                    return new MainViewModel(security.CurrentUser ?? new Models.User(), provider);
                });

                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<OrdersViewModel>();
                services.AddSingleton<WaybillsViewModel>();
                services.AddSingleton<VehiclesViewModel>();
                services.AddSingleton<DriversViewModel>();
                services.AddSingleton<CustomersViewModel>();
                services.AddSingleton<ReportsViewModel>();
                services.AddSingleton<LogViewerViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<UsersViewModel>();
                services.AddSingleton<RolesViewModel>();
                services.AddSingleton<AuditViewModel>();
                services.AddSingleton<ArchiveViewModel>();
                services.AddSingleton<NomenclatureViewModel>();
                services.AddSingleton<InventoryViewModel>();
                services.AddSingleton<FinanceViewModel>();

                services.AddTransient<LoginViewModel>();

                services.AddTransient<CustomerEditorViewModel>();
                services.AddTransient<VehicleEditorViewModel>();
                services.AddTransient<DriverEditorViewModel>();
                services.AddTransient<OrderEditorViewModel>();
                services.AddTransient<WaybillEditorViewModel>();
                services.AddTransient<UserEditorViewModel>();
                services.AddTransient<VehicleServiceRecordEditorViewModel>();
                services.AddTransient<NomenclaturePickerViewModel>();
                services.AddTransient<ProductGroupEditorViewModel>();
                services.AddTransient<ProductEditorViewModel>();
                services.AddTransient<UnitEditorViewModel>();
                services.AddTransient<InventoryDocumentEditorViewModel>();
                services.AddTransient<PaymentDocumentEditorViewModel>();
                services.AddTransient<HelpViewModel>();

                services.AddTransient<LogisticsApp.Views.Windows.LoginWindow>();

                services.AddTransient(provider =>
                {
                    var security = provider.GetRequiredService<SecurityService>();
                    return new MainWindow(security.CurrentUser ?? new Models.User());
                });

                services.AddTransient<CustomerEditWindow>();
                services.AddTransient<VehicleEditWindow>();
                services.AddTransient<DriverEditWindow>();
                services.AddTransient<OrderEditWindow>();
                services.AddTransient<WaybillEditWindow>();
                services.AddTransient<UserEditWindow>();
                services.AddTransient<VehicleServiceRecordEditWindow>();
                services.AddTransient<NomenclaturePage>();
                services.AddTransient<NomenclaturePickerWindow>();
                services.AddTransient<ProductGroupEditWindow>();
                services.AddTransient<ProductEditWindow>();
                services.AddTransient<UnitEditWindow>();
                services.AddTransient<RolesPage>();
                services.AddTransient<ArchivePage>();
                services.AddTransient<InventoryPage>();
                services.AddTransient<InventoryDocumentEditWindow>();
                services.AddTransient<FinancePage>();
                services.AddTransient<PaymentDocumentEditWindow>();
                services.AddTransient<HelpWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogisticsApp", "Logs");
        Directory.CreateDirectory(logDir);

        var settingsService = AppHost!.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Current;

        var logLevel = settings.LogLevel switch
        {
            "Debug" => Serilog.Events.LogEventLevel.Debug,
            "Info" => Serilog.Events.LogEventLevel.Information,
            "Warning" => Serilog.Events.LogEventLevel.Warning,
            "Error" => Serilog.Events.LogEventLevel.Error,
            "Fatal" => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Error
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.File(Path.Combine(logDir, "log_.txt"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            await AppHost!.StartAsync();

            ThemeManager.Current.ChangeTheme(this, $"Light.{settings.AccentColor}");

            var dbFactory = AppHost.Services.GetRequiredService<IDbContextFactory<LogisticsDbContext>>();
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                await DatabaseInitializer.InitializeAsync(context);
            }

            var authService = AppHost.Services.GetRequiredService<IAuthService>();
            var securityService = AppHost.Services.GetRequiredService<SecurityService>();

            if (authService.LoadCredentials(out string savedUser, out string savedPass))
            {
                var user = await authService.AuthenticateAsync(savedUser, savedPass);
                if (user != null)
                {
                    securityService.Initialize(user);
                    var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                    return;
                }
                else
                {
                    authService.ClearCredentials();
                }
            }

            var loginWindow = AppHost.Services.GetRequiredService<LogisticsApp.Views.Windows.LoginWindow>();
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка запуска.");
            MessageBox.Show($"Ошибка инициализации.\n{ex.Message}", "Сбой", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
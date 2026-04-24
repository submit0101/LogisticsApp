using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Theming;
using LogisticsApp.Core;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.Win32;

namespace LogisticsApp.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly NotificationService _notify;
    private readonly OverlayService _overlay;
    private readonly IDatabaseManagementService _dbManagement;
    private readonly IAuthService _authService;
    private readonly SecurityService _security;

    public IReadOnlyList<string> AvailableAccents { get; } = ["Red", "Green", "Blue", "Purple", "Orange", "Teal", "Cyan", "Amber", "Crimson", "Emerald"];
    public IReadOnlyList<string> AvailableMapProviders { get; } = ["OpenStreetMap", "GoogleMaps", "YandexMaps"];
    public IReadOnlyList<string> AvailableLogLevels { get; } = ["Debug", "Info", "Warning", "Error", "Fatal"];

    [ObservableProperty] private int _generateCount = 50;
    public int[] AvailableGenerateCounts { get; } = { 10, 50, 100, 500, 1000 };

    [ObservableProperty] private string _selectedDictionary = "Все справочники";
    public string[] AvailableDictionaries { get; } = { "Все справочники", "Контрагенты", "Автопарк", "Номенклатура" };

    public bool StrictSanitizationCheck
    {
        get => _settings.Current.StrictSanitizationCheck;
        set => _settings.Update(s => s.StrictSanitizationCheck = value);
    }

    public bool AutoRefreshDashboard
    {
        get => _settings.Current.AutoRefreshDashboard;
        set => _settings.Update(s => s.AutoRefreshDashboard = value);
    }

    public int DefaultMapZoom
    {
        get => _settings.Current.DefaultMapZoom;
        set => _settings.Update(s => s.DefaultMapZoom = value);
    }

    public string SelectedAccent
    {
        get => _settings.Current.AccentColor;
        set
        {
            _settings.Update(s => s.AccentColor = value);
            ThemeManager.Current.ChangeTheme(Application.Current, $"Light.{value}");
        }
    }

    public int SessionTimeoutMinutes
    {
        get => _settings.Current.SessionTimeoutMinutes;
        set => _settings.Update(s => s.SessionTimeoutMinutes = value);
    }

    public double MaxOverloadPercentage
    {
        get => _settings.Current.MaxOverloadPercentage;
        set => _settings.Update(s => s.MaxOverloadPercentage = value);
    }

    public int DocumentExpiryWarningDays
    {
        get => _settings.Current.DocumentExpiryWarningDays;
        set => _settings.Update(s => s.DocumentExpiryWarningDays = value);
    }

    public string DaDataApiKey
    {
        get => _settings.Current.DaDataApiKey;
        set => _settings.Update(s => s.DaDataApiKey = value);
    }

    public string MapProvider
    {
        get => _settings.Current.MapProvider;
        set => _settings.Update(s => s.MapProvider = value);
    }

    public string LogLevel
    {
        get => _settings.Current.LogLevel;
        set => _settings.Update(s => s.LogLevel = value);
    }

    public int AuditRetentionDays
    {
        get => _settings.Current.AuditRetentionDays;
        set => _settings.Update(s => s.AuditRetentionDays = value);
    }

    public int ArchiveRetentionDays
    {
        get => _settings.Current.ArchiveRetentionDays;
        set => _settings.Update(s => s.ArchiveRetentionDays = value);
    }

    public string DbServerName
    {
        get
        {
            var fullString = _settings.Current.ConnectionString ?? string.Empty;
            var parts = fullString.Split(';');
            var serverPart = parts.FirstOrDefault(p => p.StartsWith("Server=", StringComparison.OrdinalIgnoreCase));
            return serverPart?[7..] ?? @"(localdb)\mssqllocaldb";
        }
        set
        {
            _settings.Update(s => s.ConnectionString = $"Server={value};Database=LogisticsAppDB;Trusted_Connection=True;TrustServerCertificate=True;");
            OnPropertyChanged();
        }
    }

    public SettingsViewModel(
        ISettingsService settings,
        NotificationService notify,
        OverlayService overlay,
        IDatabaseManagementService dbManagement,
        IAuthService authService,
        SecurityService security)
    {
        _settings = settings;
        _notify = notify;
        _overlay = overlay;
        _dbManagement = dbManagement;
        _authService = authService;
        _security = security;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        try
        {
            bool canConnect = false;
            await _overlay.ExecuteWithOverlayAsync(async () =>
            {
                canConnect = await _dbManagement.TestConnectionAsync();
                if (!canConnect) throw new InvalidOperationException("Сервер недоступен.");
            }, "Проверка подключения...");

            if (canConnect) _notify.Success("Успешное подключение к БД");
        }
        catch (Exception ex)
        {
            _notify.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void SaveDbSettings()
    {
        _settings.Save();
        MessageBox.Show("Сохранено. Программа закроется для применения параметров.", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        var sfd = new SaveFileDialog { Filter = "SQL Server Backup (*.bak)|*.bak", FileName = $"Backup_{DateTime.Now:yyyyMMdd_HHmm}.bak" };
        if (sfd.ShowDialog() != true) return;

        try
        {
            await _overlay.ExecuteWithOverlayAsync(async () =>
            {
                await _dbManagement.BackupDatabaseAsync(sfd.FileName);
            }, "Создание резервной копии...");

            _notify.Success("Бэкап успешно создан");
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка резервного копирования: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task GenerateTestDataAsync()
    {
        if (!_security.HasPermission(AppPermission.ManageUsers))
        {
            _notify.Error("Недостаточно прав для генерации данных.");
            return;
        }

        string target = SelectedDictionary switch
        {
            "Контрагенты" => "Customers",
            "Автопарк" => "Vehicles",
            "Номенклатура" => "Nomenclature",
            _ => "All"
        };

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            await _dbManagement.GenerateTestDataAsync(GenerateCount, target);
            Application.Current.Dispatcher.Invoke(() =>
            {
                string dictName = target == "All" ? "каждого справочника" : "выбранного справочника";
                _notify.Success($"Успешно сгенерировано {GenerateCount} записей для {dictName}.");
            });
        }, "Генерация тестовых данных...");
    }

    [RelayCommand]
    private async Task WipeDatabaseAsync(object parameter)
    {
        if (!_security.HasPermission(AppPermission.ManageUsers))
        {
            _notify.Error("Недостаточно прав для очистки базы данных.");
            return;
        }

        if (parameter is not PasswordBox passwordBox || string.IsNullOrWhiteSpace(passwordBox.Password))
        {
            _notify.Warning("Введите пароль для подтверждения сброса БД.");
            return;
        }

        string password = passwordBox.Password;

        await _overlay.ExecuteWithOverlayAsync(async () =>
        {
            var authUser = await _authService.AuthenticateAsync(_security.CurrentUser?.Login ?? "", password);

            if (authUser == null)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error("Неверный пароль. Очистка отменена."));
                return;
            }

            try
            {
                await _dbManagement.WipeDatabaseAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    passwordBox.Clear();
                    _notify.Success("База данных успешно очищена.");
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error($"Ошибка при очистке БД: {ex.Message}"));
            }

        }, "Полное удаление данных из БД...");
    }
}
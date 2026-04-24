using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using LogisticsApp.Messages;

namespace LogisticsApp.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Update(Action<AppSettings> updateAction);
    void Save();
}

public sealed class AppSettings
{
    public string AccentColor { get; set; } = "Red";
    public bool StrictSanitizationCheck { get; set; } = true;
    public bool AutoRefreshDashboard { get; set; } = true;
    public int DefaultMapZoom { get; set; } = 12;
    public string ConnectionString { get; set; } = @"Server=(localdb)\mssqllocaldb;Database=LogisticsAppDB;Trusted_Connection=True;TrustServerCertificate=True;";
    public string LastLogin { get; set; } = string.Empty;
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 850;
    public bool IsMaximized { get; set; } = false;
    public int SessionTimeoutMinutes { get; set; } = 15;
    public int AuditRetentionDays { get; set; } = 90;
    public string DaDataApiKey { get; set; } = "d3d669be25f078cb7de5544574d6d9f759b58ddf";
    public string YandexMapsApiKey { get; set; } = "091a1f75-fa46-469a-8856-57fb27cd5903";
    public string MapProvider { get; set; } = "YandexMaps";
    public double MaxOverloadPercentage { get; set; } = 2.0;
    public int DocumentExpiryWarningDays { get; set; } = 14;
    public string LogLevel { get; set; } = "Error";
    public int ArchiveRetentionDays { get; set; } = 30;
}

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly IMessenger _messenger;
    private readonly ReaderWriterLockSlim _lock = new();
    private AppSettings _current = null!;

    public AppSettings Current
    {
        get
        {
            _lock.EnterReadLock();
            try { return _current; }
            finally { _lock.ExitReadLock(); }
        }
    }

    public SettingsService(IMessenger messenger)
    {
        _messenger = messenger;
        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogisticsApp");
        Directory.CreateDirectory(appDataFolder);
        _settingsFilePath = Path.Combine(appDataFolder, "settings.json");
        LoadInternal();
    }

    public void Update(Action<AppSettings> updateAction)
    {
        _lock.EnterWriteLock();
        try
        {
            updateAction(_current);
            SaveInternal();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        _messenger.Send(new SettingsChangedMessage(_current));
    }

    public void Save()
    {
        _lock.EnterWriteLock();
        try { SaveInternal(); }
        finally { _lock.ExitWriteLock(); }
    }

    private void LoadInternal()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                return;
            }
            catch { }
        }
        _current = new AppSettings();
        SaveInternal();
    }

    private void SaveInternal()
    {
        var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Models;
using LogisticsApp.Services;

namespace LogisticsApp.ViewModels;

public sealed partial class LogViewerViewModel : ViewModelBase
{
    private readonly ILogReaderService _logReader;
    private readonly NotificationService _notify;

    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _loadLogsCts;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<string> _availableFiles = new ObservableCollection<string>();
    [ObservableProperty] private ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedLogLevel = "Все";
    [ObservableProperty] private string? _selectedFile;

    public IReadOnlyList<string> LogLevels { get; } = new List<string> { "Все", "INF", "WRN", "ERR", "FTL", "DBG" };

    public LogViewerViewModel(ILogReaderService logReader, NotificationService notify)
    {
        _logReader = logReader;
        _notify = notify;
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() => _ = InitializeAsync());
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var files = await _logReader.GetAvailableLogFilesAsync().ConfigureAwait(false);

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableFiles.Clear();
                    foreach (var file in files) AvailableFiles.Add(file);

                    if (AvailableFiles.Count > 0)
                    {
                        SelectedFile = AvailableFiles[0];
                    }
                });
            }
        }
        catch (Exception ex)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error($"Сбой загрузки списка журналов: {ex.Message}"));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedFileChanged(string? value) => TriggerLogReload();
    partial void OnSelectedLogLevelChanged(string value) => TriggerLogReload();

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        _ = ReloadWithDebounceAsync(_searchDebounceCts.Token);
    }

    private async Task ReloadWithDebounceAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(500, token).ConfigureAwait(false);
            TriggerLogReload();
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private void TriggerLogReload()
    {
        _loadLogsCts?.Cancel();
        _loadLogsCts = new CancellationTokenSource();
        _ = LoadLogsCoreAsync(_loadLogsCts.Token);
    }

    private async Task LoadLogsCoreAsync(CancellationToken token)
    {
        if (string.IsNullOrEmpty(SelectedFile)) return;

        IsLoading = true;
        try
        {
            var buffer = new List<LogEntry>();
            int limit = 5000;

            await foreach (var entry in _logReader.ReadAndFilterLogsAsync(SelectedFile, SearchText, SelectedLogLevel, token).ConfigureAwait(false))
            {
                buffer.Add(entry);
                if (buffer.Count >= limit) break;
            }

            buffer.Reverse();
            if (token.IsCancellationRequested) return;

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Clear();
                    foreach (var entry in buffer)
                    {
                        Logs.Add(entry);
                    }
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() => _notify.Error($"Сбой чтения журнала: {ex.Message}"));
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedLogLevel = "Все";
    }
}
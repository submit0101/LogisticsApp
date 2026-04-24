using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogisticsApp.Core;
using LogisticsApp.Data;
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LogisticsApp.ViewModels;

public sealed partial class AuditViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;

    [ObservableProperty] private ObservableCollection<AuditLog> _auditLogs = [];
    [ObservableProperty] private bool _isLoading;

    public AuditViewModel(IDbContextFactory<LogisticsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;

        _ = LoadAuditAsync();
    }

    public async Task InitializeAsync() => await LoadAuditAsync();

    [RelayCommand]
    private async Task LoadAuditAsync()
    {
        IsLoading = true;
        try
        {
            var logs = new List<AuditLog>();
            await foreach (var log in FetchAuditLogsAsync().ConfigureAwait(false))
            {
                logs.Add(log);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                AuditLogs.Clear();
                foreach (var log in logs) AuditLogs.Add(log);
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async IAsyncEnumerable<AuditLog> FetchAuditLogsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var query = context.AuditLogs.Include(a => a.User).OrderByDescending(a => a.Timestamp).Take(200).AsNoTracking().AsAsyncEnumerable();

        await foreach (var log in query.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return log;
        }
    }
}
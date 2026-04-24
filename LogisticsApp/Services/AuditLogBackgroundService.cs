using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogisticsApp.Services;

public class AuditLogBackgroundService : BackgroundService
{
    private readonly AuditLogChannel _channel;
    private readonly IServiceProvider _serviceProvider;

    public AuditLogBackgroundService(AuditLogChannel channel, IServiceProvider serviceProvider)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditLog>();
        DateTime lastCleanup = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if ((DateTime.Now - lastCleanup).TotalHours >= 24)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LogisticsDbContext>>();

                    using var context = await factory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);
                    var thresholdDate = DateTime.Now.AddDays(-settings.Current.AuditRetentionDays);

                    await context.AuditLogs
                                 .Where(l => l.Timestamp < thresholdDate)
                                 .ExecuteDeleteAsync(stoppingToken)
                                 .ConfigureAwait(false);

                    lastCleanup = DateTime.Now;
                }

                var hasItem = await _channel.Channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false);
                if (!hasItem) continue;

                while (batch.Count < 100 && _channel.Channel.Reader.TryRead(out var log))
                {
                    batch.Add(log);
                }

                if (batch.Count > 0)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LogisticsDbContext>>();
                    using var context = await factory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);

                    await context.AuditLogs.AddRangeAsync(batch, stoppingToken).ConfigureAwait(false);
                    await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

                    batch.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
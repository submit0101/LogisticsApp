using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogisticsApp.Services;

public sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public OutboxProcessorBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LogisticsDbContext>>();
                using var context = await factory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);

                var messages = await context.Set<OutboxMessage>()
                    .Where(m => m.ProcessedAt == null)
                    .OrderBy(m => m.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (messages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            if (message.Type == "AuditLog")
                            {
                                var auditLog = JsonSerializer.Deserialize<AuditLog>(message.Payload);
                                if (auditLog != null)
                                {
                                    auditLog.LogID = 0;
                                    auditLog.User = null;
                                    context.AuditLogs.Add(auditLog);
                                }
                            }
                            message.ProcessedAt = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            message.Error = ex.Message;
                            message.ProcessedAt = DateTime.Now;
                        }
                    }

                    await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
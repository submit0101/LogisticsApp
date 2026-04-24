using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LogisticsApp.Services;

public class ArchiveCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public ArchiveCleanupBackgroundService(IServiceProvider serviceProvider)
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
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LogisticsDbContext>>();

                using var context = await factory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);
                var thresholdDate = DateTime.Now.AddDays(-settings.Current.ArchiveRetentionDays);

                int deletedCount = 0;

                deletedCount += await context.Customers.IgnoreQueryFilters().Where(c => c.IsDeleted && c.DeletedAt < thresholdDate).ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);
                deletedCount += await context.Drivers.IgnoreQueryFilters().Where(d => d.IsDeleted && d.DeletedAt < thresholdDate).ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);
                deletedCount += await context.Vehicles.IgnoreQueryFilters().Where(v => v.IsDeleted && v.DeletedAt < thresholdDate).ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);
                deletedCount += await context.Orders.IgnoreQueryFilters().Where(o => o.IsDeleted && o.DeletedAt < thresholdDate).ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);
                deletedCount += await context.Products.IgnoreQueryFilters().Where(p => p.IsDeleted && p.DeletedAt < thresholdDate).ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);
                deletedCount += await context.Waybills.IgnoreQueryFilters().Where(w => w.IsDeleted && w.DeletedAt < thresholdDate).ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);

                if (deletedCount > 0)
                {
                    Log.Information("Очистка архива: физически удалено {Count} старых записей.", deletedCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при очистке архива (корзины).");
            }

            await Task.Delay(TimeSpan.FromHours(12), stoppingToken).ConfigureAwait(false);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models.DTOs.Reports;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Services;

public sealed class ReportDataService : IReportDataService
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;

    public ReportDataService(IDbContextFactory<LogisticsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<WaybillRegistryDto>> GetWaybillsRegistryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var endDate = to.Date.AddDays(1).AddTicks(-1);

        return await context.Waybills
            .AsNoTracking()
            .Where(w => w.DateCreate >= from.Date && w.DateCreate <= endDate)
            .Select(w => new WaybillRegistryDto
            {
                WaybillID = w.WaybillID,
                DateCreate = w.DateCreate,
                DriverName = w.Driver != null ? w.Driver.LastName + " " + w.Driver.FirstName : "Не назначен",
                VehicleReg = w.Vehicle != null ? w.Vehicle.RegNumber : "Не назначено",
                Status = w.Status.ToString(),
                Distance = w.TotalDistance,
                PointsCount = w.Points.Count,
                TotalWeight = w.Points.Sum(p => p.Order != null ? p.Order.WeightKG : 0)
            })
            .OrderBy(dto => dto.DateCreate)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
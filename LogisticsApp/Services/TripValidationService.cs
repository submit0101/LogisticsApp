using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogisticsApp.Core.Specifications;
using LogisticsApp.Core.Specifications.Trips;
using LogisticsApp.Data;
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Services;

public sealed class TripValidationService
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly IReadOnlyList<ISpecification<TripValidationContext>> _specifications;

    public TripValidationService(IDbContextFactory<LogisticsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _specifications = new List<ISpecification<TripValidationContext>>
        {
            new DriverLicenseSpecification(),
            new DriverMedicalCertificateSpecification(),
            new VehicleSanitizationSpecification(),
            new VehicleMaintenanceSpecification(),
            new VehicleCapacitySpecification()
        };
    }

    public async Task<SpecificationResult> ValidateTripAsync(Waybill waybill)
    {
        await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

        var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.VehicleID == waybill.VehicleID).ConfigureAwait(false);
        var driver = await context.Drivers.FirstOrDefaultAsync(d => d.DriverID == waybill.DriverID).ConfigureAwait(false);

        if (vehicle == null || driver == null)
            return SpecificationResult.Fail("Транспортное средство или водитель не найдены в базе данных.");

        var lastRecord = await context.VehicleServiceRecords
            .Where(r => r.VehicleID == vehicle.VehicleID)
            .OrderByDescending(r => r.OdometerReading)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        int lastMaintenanceMileage = lastRecord?.OdometerReading ?? 0;

        var orderIds = waybill.Points.Select(p => p.OrderID).ToList();
        var orders = await context.Orders.Where(o => orderIds.Contains(o.OrderID)).ToListAsync().ConfigureAwait(false);

        double totalWeight = (double)orders.Sum(o => o.WeightKG);

        var validationContext = new TripValidationContext
        {
            Driver = driver,
            Vehicle = vehicle,
            ExpectedReturnDate = waybill.ExpectedArrivalTime ?? DateTime.Now.AddDays(Math.Ceiling(waybill.TotalDistance / 500.0)),
            ExpectedDistance = waybill.TotalDistance,
            LastMaintenanceMileage = lastMaintenanceMileage,
            MaintenanceIntervalKM = 20000,
            TotalCargoWeightKG = totalWeight
        };

        var results = _specifications.Select(spec => spec.IsSatisfiedBy(validationContext)).ToList();
        return SpecificationResult.Combine(results);
    }
}
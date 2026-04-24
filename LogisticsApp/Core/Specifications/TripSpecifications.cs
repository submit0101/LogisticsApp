using System;
using LogisticsApp.Models;

namespace LogisticsApp.Core.Specifications.Trips;

public sealed class TripValidationContext
{
    public Driver Driver { get; init; } = null!;
    public Vehicle Vehicle { get; init; } = null!;
    public DateTime ExpectedReturnDate { get; init; }
    public double ExpectedDistance { get; init; }
    public int LastMaintenanceMileage { get; init; }
    public int MaintenanceIntervalKM { get; init; }
    public double TotalCargoWeightKG { get; init; }
}

public sealed class DriverLicenseSpecification : ISpecification<TripValidationContext>
{
    public SpecificationResult IsSatisfiedBy(TripValidationContext context)
    {
        if (context.Driver.LicenseExpirationDate.Date < context.ExpectedReturnDate.Date)
            return SpecificationResult.Fail($"Срок действия ВУ водителя ({context.Driver.FullName}) истекает до планового завершения рейса.");

        if ((context.Driver.LicenseExpirationDate.Date - context.ExpectedReturnDate.Date).TotalDays <= 7)
            return SpecificationResult.Warn($"Срок действия ВУ водителя ({context.Driver.FullName}) истекает менее чем через 7 дней после возвращения.");

        return SpecificationResult.Success();
    }
}

public sealed class DriverMedicalCertificateSpecification : ISpecification<TripValidationContext>
{
    public SpecificationResult IsSatisfiedBy(TripValidationContext context)
    {
        if (!context.Driver.MedicalCertificateExpiration.HasValue)
            return SpecificationResult.Warn($"У водителя ({context.Driver.FullName}) не указан срок действия медицинской справки.");

        if (context.Driver.MedicalCertificateExpiration.Value.Date < context.ExpectedReturnDate.Date)
            return SpecificationResult.Fail($"Срок действия медсправки водителя ({context.Driver.FullName}) истекает до планового завершения рейса.");

        return SpecificationResult.Success();
    }
}

public sealed class VehicleSanitizationSpecification : ISpecification<TripValidationContext>
{
    public SpecificationResult IsSatisfiedBy(TripValidationContext context)
    {
        var daysSinceSanitization = (context.ExpectedReturnDate.Date - context.Vehicle.SanitizationDate.Date).TotalDays;
        if (daysSinceSanitization > 30)
            return SpecificationResult.Fail($"Срок действия санитарной обработки ТС ({context.Vehicle.RegNumber}) истечет к моменту завершения рейса (>30 дней).");

        if (daysSinceSanitization > 25)
            return SpecificationResult.Warn($"Срок действия санитарной обработки ТС ({context.Vehicle.RegNumber}) истекает (прошло {daysSinceSanitization} дней из 30).");

        return SpecificationResult.Success();
    }
}

public sealed class VehicleMaintenanceSpecification : ISpecification<TripValidationContext>
{
    public SpecificationResult IsSatisfiedBy(TripValidationContext context)
    {
        if (context.MaintenanceIntervalKM <= 0) return SpecificationResult.Success();

        double projectedMileage = context.Vehicle.Mileage + context.ExpectedDistance;
        double nextMaintenanceMileage = context.LastMaintenanceMileage + context.MaintenanceIntervalKM;

        if (projectedMileage > nextMaintenanceMileage)
            return SpecificationResult.Fail($"Планируемый пробег ({projectedMileage:F0} км) превысит межсервисный интервал ТО ТС ({context.Vehicle.RegNumber}). Лимит: {nextMaintenanceMileage} км.");

        if (nextMaintenanceMileage - projectedMileage <= 500)
            return SpecificationResult.Warn($"ТС ({context.Vehicle.RegNumber}) приближается к плановому ТО. Остаток после рейса: {nextMaintenanceMileage - projectedMileage:F0} км.");

        return SpecificationResult.Success();
    }
}

public sealed class VehicleCapacitySpecification : ISpecification<TripValidationContext>
{
    public SpecificationResult IsSatisfiedBy(TripValidationContext context)
    {
        if (context.Vehicle.CapacityKG > 0 && context.TotalCargoWeightKG > context.Vehicle.CapacityKG * 1.05)
            return SpecificationResult.Fail($"Физический перегруз ТС ({context.Vehicle.RegNumber}). Вес груза: {context.TotalCargoWeightKG:F0} кг, г/п: {context.Vehicle.CapacityKG:F0} кг.");

        if (context.Vehicle.CapacityKG > 0 && context.TotalCargoWeightKG > context.Vehicle.CapacityKG)
            return SpecificationResult.Warn($"Вес груза ({context.TotalCargoWeightKG:F0} кг) превышает номинальную г/п ТС ({context.Vehicle.RegNumber}), но находится в пределах 5% допуска.");

        return SpecificationResult.Success();
    }
}
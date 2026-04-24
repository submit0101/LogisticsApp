using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Services;

public sealed class WaybillDispatchService
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;
    private readonly InventoryService _inventoryService;
    private readonly SecurityService _security;

    public WaybillDispatchService(IDbContextFactory<LogisticsDbContext> dbFactory, InventoryService inventoryService, SecurityService security)
    {
        _dbFactory = dbFactory;
        _inventoryService = inventoryService;
        _security = security;
    }

    public async Task SaveWaybillAsync(Waybill waybill, bool postDocument, Vehicle? selectedVehicle)
    {
        await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);

        if (postDocument && selectedVehicle != null)
        {
            // SENIOR FIX: Хардкорные блокировки на уровне службы БД (Защита от сбоев UI)

            if (waybill.TotalDistance <= 0)
                throw new InvalidOperationException("Блокировка БД: Дистанция маршрута равна нулю. Невозможно рассчитать норму ГСМ.");

            double totalFuelAvailable = (waybill.FuelOut ?? 0) + waybill.FuelTickets.Sum(t => t.VolumeLiters);
            double requiredFuelWithReserve = waybill.CalculatedFuelConsumption * 1.05;

            if (totalFuelAvailable < requiredFuelWithReserve)
                throw new InvalidOperationException($"Блокировка БД: Недостаточно ГСМ на рейс. Дефицит: {(requiredFuelWithReserve - totalFuelAvailable):F1} л.");

            var orderIds = waybill.Points.Select(p => p.OrderID).ToList();
            var totalWeight = await context.Orders.Where(o => orderIds.Contains(o.OrderID)).SumAsync(o => o.WeightKG).ConfigureAwait(false);
            if (selectedVehicle.CapacityKG > 0 && totalWeight > selectedVehicle.CapacityKG * 1.05)
                throw new InvalidOperationException($"Блокировка БД: Физический перегруз ТС. Вес ({totalWeight} кг) превышает грузоподъемность с учетом перегруза.");

            if (waybill.Status == WaybillStatus.Active || waybill.Status == WaybillStatus.Planned)
            {
                bool isBusy = await context.Waybills.AnyAsync(w =>
                    w.VehicleID == selectedVehicle.VehicleID &&
                    w.WaybillID != waybill.WaybillID &&
                    (w.Status == WaybillStatus.Active || w.Status == WaybillStatus.Planned)).ConfigureAwait(false);

                if (isBusy) throw new InvalidOperationException($"Блокировка БД: Автомобиль {selectedVehicle.RegNumber} уже назначен на другой активный или запланированный рейс.");
            }

            if (waybill.Status == WaybillStatus.Completed)
            {
                if (waybill.OdometerIn < waybill.OdometerOut)
                    throw new InvalidOperationException("Блокировка БД: Показания одометра по возвращении не могут быть меньше показаний при выезде (попытка скрутки пробега).");
            }
        }

        await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

        if (waybill.WaybillID == 0)
        {
            waybill.Vehicle = null;
            waybill.Driver = null;

            var newPoints = waybill.Points.Select(p => new WaybillPoint { OrderID = p.OrderID, SequenceNumber = p.SequenceNumber, Status = p.Status, DeliveredWeightKG = p.DeliveredWeightKG, Waybill = waybill }).ToList();
            var newTickets = waybill.FuelTickets.Select(t => new FuelTicket { TicketDate = t.TicketDate, VolumeLiters = t.VolumeLiters, Amount = t.Amount, TicketNumber = t.TicketNumber, FuelType = t.FuelType, PricePerLiter = t.PricePerLiter, Waybill = waybill }).ToList();

            waybill.Points = newPoints;
            waybill.FuelTickets = newTickets;

            context.Waybills.Add(waybill);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            var originalWaybill = await context.Waybills.Include(w => w.Points).Include(w => w.FuelTickets).FirstOrDefaultAsync(w => w.WaybillID == waybill.WaybillID).ConfigureAwait(false);
            if (originalWaybill is not null)
            {
                context.Entry(originalWaybill).CurrentValues.SetValues(waybill);

                var pointsToRemove = originalWaybill.Points.Where(p => !waybill.Points.Any(np => np.OrderID == p.OrderID)).ToList();
                foreach (var p in pointsToRemove)
                {
                    var o = await context.Orders.FindAsync(p.OrderID).ConfigureAwait(false);
                    if (o is not null) o.Status = "New";
                    context.WaybillPoints.Remove(p);
                }

                foreach (var np in waybill.Points)
                {
                    var ep = originalWaybill.Points.FirstOrDefault(p => p.OrderID == np.OrderID);
                    if (ep is not null)
                    {
                        ep.SequenceNumber = np.SequenceNumber;
                        ep.Status = np.Status;
                        ep.DeliveredWeightKG = np.DeliveredWeightKG;
                        context.Entry(ep).State = EntityState.Modified;
                    }
                    else
                    {
                        context.WaybillPoints.Add(new WaybillPoint { WaybillID = originalWaybill.WaybillID, OrderID = np.OrderID, SequenceNumber = np.SequenceNumber, Status = np.Status, DeliveredWeightKG = np.DeliveredWeightKG });
                    }
                }

                var ticketsToRemove = originalWaybill.FuelTickets.Where(t => !waybill.FuelTickets.Any(nt => nt.TicketID == t.TicketID && nt.TicketID != 0)).ToList();
                foreach (var t in ticketsToRemove) context.FuelTickets.Remove(t);

                foreach (var nt in waybill.FuelTickets)
                {
                    if (nt.TicketID == 0)
                    {
                        context.FuelTickets.Add(new FuelTicket { WaybillID = originalWaybill.WaybillID, TicketDate = nt.TicketDate, VolumeLiters = nt.VolumeLiters, Amount = nt.Amount, TicketNumber = nt.TicketNumber, FuelType = nt.FuelType, PricePerLiter = nt.PricePerLiter });
                    }
                }

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        if (postDocument)
        {
            foreach (var point in waybill.Points)
            {
                var order = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderID == point.OrderID).ConfigureAwait(false);
                if (order is not null)
                {
                    if (waybill.Status == WaybillStatus.Cancelled) order.Status = "New";
                    else if (waybill.Status == WaybillStatus.Completed)
                    {
                        if (point.Status == WaybillPointStatus.Delivered || point.Status == WaybillPointStatus.PartiallyDelivered)
                        {
                            order.Status = "Delivered";
                            var existingSettlement = await context.MutualSettlements.FirstOrDefaultAsync(ms => ms.OrderID == order.OrderID && ms.Type == MutualSettlementType.DebtIncrease).ConfigureAwait(false);
                            if (existingSettlement == null)
                            {
                                decimal totalSum = order.Items.Sum(i => i.TotalPrice);

                                if (point.Status == WaybillPointStatus.PartiallyDelivered && point.DeliveredWeightKG.HasValue && order.WeightKG > 0)
                                {
                                    decimal deliveryRatio = (decimal)(point.DeliveredWeightKG.Value / order.WeightKG);
                                    totalSum = Math.Round(totalSum * deliveryRatio, 2);
                                }

                                context.MutualSettlements.Add(new MutualSettlement { CustomerID = order.CustomerID, OrderID = order.OrderID, Date = DateTime.Now, Amount = totalSum, Type = MutualSettlementType.DebtIncrease, Description = $"Отгрузка (П/Л №{waybill.WaybillID})" });
                            }
                        }
                        else order.Status = "New";
                    }
                    else if (waybill.Status == WaybillStatus.Active) order.Status = "InTransit";
                    else order.Status = "Planned";

                    context.Orders.Update(order);
                }
            }

            if (waybill.Status == WaybillStatus.Active || waybill.Status == WaybillStatus.Completed) await _inventoryService.WriteOffWaybillAsync(context, waybill.WaybillID);
            else await _inventoryService.RevertWaybillWriteOffAsync(context, waybill.WaybillID);

            if (waybill.Status == WaybillStatus.Completed && selectedVehicle != null)
            {
                var v = await context.Vehicles.FindAsync(selectedVehicle.VehicleID).ConfigureAwait(false);
                if (v != null)
                {
                    v.Mileage = waybill.OdometerIn ?? v.Mileage;
                    v.CurrentFuelLevel = waybill.FuelIn ?? v.CurrentFuelLevel;
                    context.Vehicles.Update(v);
                }
            }
        }
        else
        {
            await _inventoryService.RevertWaybillWriteOffAsync(context, waybill.WaybillID);
        }

        var actionStr = postDocument ? "Проведение" : (waybill.WaybillID == 0 ? "Создание" : "Изменение");
        var auditLog = new AuditLog { Action = actionStr, EntityName = "Путевые листы", Details = $"Сотрудник сохранил П/Л. Авто: {selectedVehicle?.RegNumber}. Проведен: {postDocument}, Статус: {waybill.Status}", Timestamp = DateTime.Now, UserID = _security.CurrentUser?.UserID };
        context.AuditLogs.Add(auditLog);

        await context.SaveChangesAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
    }

    public async Task UnpostWaybillAsync(Waybill currentWaybill)
    {
        await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

        var originalWaybill = await context.Waybills.Include(w => w.Points).Include(w => w.FuelTickets).FirstOrDefaultAsync(w => w.WaybillID == currentWaybill.WaybillID).ConfigureAwait(false);
        if (originalWaybill != null)
        {
            if (originalWaybill.Status == WaybillStatus.Completed)
            {
                var v = await context.Vehicles.FindAsync(originalWaybill.VehicleID).ConfigureAwait(false);
                if (v != null)
                {
                    if (v.Mileage > originalWaybill.OdometerIn)
                    {
                        throw new InvalidOperationException("Блокировка: существуют более новые рейсы по данному автомобилю. Отмена проведения невозможна.");
                    }
                    v.Mileage = originalWaybill.OdometerOut ?? v.Mileage;
                    v.CurrentFuelLevel = originalWaybill.FuelOut ?? v.CurrentFuelLevel;
                    context.Vehicles.Update(v);
                }
            }

            await _inventoryService.RevertWaybillWriteOffAsync(context, originalWaybill.WaybillID);

            originalWaybill.IsPosted = false;
            originalWaybill.Status = WaybillStatus.Draft;
            originalWaybill.DepartureTime = null;
            originalWaybill.ExpectedArrivalTime = null;
            originalWaybill.HasIncident = false;

            foreach (var p in originalWaybill.Points)
            {
                var o = await context.Orders.FindAsync(p.OrderID).ConfigureAwait(false);
                if (o != null)
                {
                    o.Status = "New";
                    context.Orders.Update(o);

                    var settlement = await context.MutualSettlements.FirstOrDefaultAsync(ms => ms.OrderID == o.OrderID && ms.Type == MutualSettlementType.DebtIncrease).ConfigureAwait(false);
                    if (settlement != null) context.MutualSettlements.Remove(settlement);
                }
            }

            var auditLog = new AuditLog
            {
                Action = "Отмена проведения",
                EntityName = "Путевые листы",
                Details = $"Отмена проведения путевого листа {originalWaybill.WaybillID}. Заказы освобождены, резервы восстановлены, метрики ТС откачены.",
                Timestamp = DateTime.Now,
                UserID = _security.CurrentUser?.UserID
            };

            context.AuditLogs.Add(auditLog);
            await context.SaveChangesAsync().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
    }

    public async Task LogIncidentAsync(int waybillId, string? incidentType, string? incidentDescription, int delayMinutes)
    {
        await using var context = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        context.AuditLogs.Add(new AuditLog
        {
            Action = "ЧРЕЗВЫЧАЙНОЕ ПРОИСШЕСТВИЕ",
            EntityName = "Путевые листы",
            Details = $"ПЛ №{waybillId}. {incidentType}: {incidentDescription}. Задержка: +{delayMinutes} мин.",
            Timestamp = DateTime.Now,
            UserID = _security.CurrentUser?.UserID
        });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
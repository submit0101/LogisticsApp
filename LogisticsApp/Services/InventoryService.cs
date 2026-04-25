using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Services;

public sealed class InventoryService
{
    private readonly SecurityService _security;

    public InventoryService(SecurityService security)
    {
        _security = security;
    }

    public InventoryService()
    {
        _security = new SecurityService();
    }

    public async Task<int> GetDefaultWarehouseIdAsync(LogisticsDbContext context)
    {
        var warehouse = await context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.WarehouseID).FirstOrDefaultAsync();
        if (warehouse == null) throw new InvalidOperationException("Критическая ошибка: В системе не заведено ни одного склада.");
        return warehouse.WarehouseID;
    }

    public async Task<Dictionary<int, double>> GetAvailableStockAsync(LogisticsDbContext context, IEnumerable<int> productIds, int warehouseId)
    {
        var ids = productIds.Distinct().ToList();
        var balances = await context.InventoryTransactions
            .Where(t => ids.Contains(t.ProductID) && t.WarehouseID == warehouseId)
            .GroupBy(t => t.ProductID)
            .Select(g => new { ProductID = g.Key, Available = g.Sum(t => t.Quantity) })
            .ToDictionaryAsync(x => x.ProductID, x => (double)x.Available);
        return ids.ToDictionary(id => id, id => balances.TryGetValue(id, out var b) ? b : 0);
    }

    public async Task EnsureStockSufficientAsync(LogisticsDbContext context, IEnumerable<OrderItem> items, int warehouseId)
    {
        var productIds = items.Select(i => i.ProductID).Distinct().ToList();
        var balances = await GetAvailableStockAsync(context, productIds, warehouseId);
        var required = items.GroupBy(i => i.ProductID).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        foreach (var req in required)
        {
            if (!balances.TryGetValue(req.Key, out double available) || available < req.Value)
            {
                var product = await context.Products.FindAsync(req.Key);
                throw new InvalidOperationException($"Блокировка проведения: Дефицит остатков для '{product?.Name ?? req.Key.ToString()}'. Запрошено: {req.Value}, Доступно: {available}.");
            }
        }
    }

    public async Task AllocateOrderAsync(LogisticsDbContext context, Order targetOrder)
    {
        var affectedOrders = new HashSet<int> { targetOrder.OrderID };
        var orderItems = await context.OrderItems.Where(i => i.OrderID == targetOrder.OrderID).ToListAsync().ConfigureAwait(false);

        foreach (var item in orderItems)
        {
            double requiredQty = item.Quantity;

            var existingReserves = await context.InventoryTransactions
                .Where(t => t.OrderID == targetOrder.OrderID && t.ProductID == item.ProductID && t.IsReserve)
                .SumAsync(t => t.Quantity).ConfigureAwait(false);

            double currentlyReserved = Math.Abs(existingReserves);
            if (currentlyReserved >= requiredQty) continue;

            double needed = requiredQty - currentlyReserved;

            var allTransactions = await context.InventoryTransactions
                .Where(t => t.ProductID == item.ProductID)
                .ToListAsync().ConfigureAwait(false);

            double totalIn = allTransactions.Where(t => !t.IsReserve && t.Quantity > 0).Sum(t => t.Quantity);
            double totalOut = Math.Abs(allTransactions.Where(t => !t.IsReserve && t.Quantity < 0).Sum(t => t.Quantity));
            double totalReserved = Math.Abs(allTransactions.Where(t => t.IsReserve && t.Quantity < 0).Sum(t => t.Quantity));

            double physicalStock = totalIn - totalOut;
            double freeStock = physicalStock - totalReserved;

            if (freeStock >= needed)
            {
                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    Timestamp = DateTime.Now,
                    ProductID = item.ProductID,
                    WarehouseID = targetOrder.WarehouseID ?? 1,
                    Quantity = (int)-Math.Round(needed),
                    IsReserve = true,
                    OrderID = targetOrder.OrderID,
                    SourceDocument = "Order",
                    SourceDocumentID = targetOrder.OrderID
                });
                continue;
            }

            if (freeStock > 0)
            {
                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    Timestamp = DateTime.Now,
                    ProductID = item.ProductID,
                    WarehouseID = targetOrder.WarehouseID ?? 1,
                    Quantity = (int)-Math.Round(freeStock),
                    IsReserve = true,
                    OrderID = targetOrder.OrderID,
                    SourceDocument = "Order",
                    SourceDocumentID = targetOrder.OrderID
                });
                needed -= freeStock;
            }

            if (targetOrder.Priority > OrderPriority.Low && needed > 0)
            {
                // ИСПРАВЛЕНИЕ: Выгружаем данные из БД в память перед группировкой
                var rawTransactions = await context.InventoryTransactions
                    .Include(t => t.Order)
                    .Where(t => t.ProductID == item.ProductID &&
                                t.IsReserve &&
                                t.Quantity < 0 &&
                                t.OrderID != null &&
                                t.Order != null &&
                                t.Order.Priority < targetOrder.Priority &&
                                t.Order.Status != "InTransit" &&
                                t.Order.Status != "Delivered")
                    .ToListAsync().ConfigureAwait(false);

                // ИСПРАВЛЕНИЕ: Выполняем группировку локально
                var lowerPriorityReserves = rawTransactions
                    .GroupBy(t => t.OrderID)
                    .Select(g => new
                    {
                        Order = g.First().Order,
                        ReservedAmount = Math.Abs(g.Sum(x => x.Quantity)),
                        WarehouseID = g.First().WarehouseID
                    })
                    .Where(x => x.ReservedAmount > 0)
                    .OrderBy(x => x.Order!.Priority)
                    .ThenByDescending(x => x.Order!.OrderDate)
                    .ToList();

                foreach (var reserve in lowerPriorityReserves)
                {
                    if (needed <= 0) break;

                    double stolen = Math.Min(reserve.ReservedAmount, needed);

                    context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        Timestamp = DateTime.Now,
                        ProductID = item.ProductID,
                        WarehouseID = reserve.WarehouseID,
                        Quantity = (int)Math.Round(stolen),
                        IsReserve = true,
                        OrderID = reserve.Order!.OrderID,
                        SourceDocument = "AllocationSteal",
                        SourceDocumentID = reserve.Order.OrderID
                    });

                    context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        Timestamp = DateTime.Now,
                        ProductID = item.ProductID,
                        WarehouseID = targetOrder.WarehouseID ?? 1,
                        Quantity = (int)-Math.Round(stolen),
                        IsReserve = true,
                        OrderID = targetOrder.OrderID,
                        SourceDocument = "AllocationGrant",
                        SourceDocumentID = targetOrder.OrderID
                    });

                    context.AuditLogs.Add(new AuditLog
                    {
                        Action = "Срыв резерва WMS",
                        EntityName = "Inventory",
                        Details = $"Система забрала {stolen} ед. (Товар ID {item.ProductID}) у заказа {reserve.Order.OrderID} (Приоритет: {reserve.Order.Priority}) в пользу VIP заказа {targetOrder.OrderID} (Приоритет: {targetOrder.Priority}).",
                        Timestamp = DateTime.Now,
                        UserID = _security.CurrentUser?.UserID
                    });

                    needed -= stolen;
                    affectedOrders.Add(reserve.Order.OrderID);
                }
            }
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        foreach (var orderId in affectedOrders)
        {
            await RecalculateOrderFulfillmentStatusAsync(context, orderId).ConfigureAwait(false);
        }
    }

    public async Task ReleaseOrderAllocationAsync(LogisticsDbContext context, int orderId)
    {
        var reserves = await context.InventoryTransactions
            .Where(t => t.OrderID == orderId && t.IsReserve)
            .ToListAsync().ConfigureAwait(false);

        double totalReserved = reserves.Sum(t => t.Quantity);
        if (Math.Abs(totalReserved) > 0)
        {
            var grouped = reserves.GroupBy(t => new { t.ProductID, t.WarehouseID }).ToList();
            foreach (var group in grouped)
            {
                double qty = Math.Abs(group.Sum(x => x.Quantity));
                if (qty > 0)
                {
                    context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        Timestamp = DateTime.Now,
                        ProductID = group.Key.ProductID,
                        WarehouseID = group.Key.WarehouseID,
                        Quantity = (int)Math.Round(qty),
                        IsReserve = true,
                        OrderID = orderId,
                        SourceDocument = "ReleaseReserve",
                        SourceDocumentID = orderId
                    });
                }
            }
            await context.SaveChangesAsync().ConfigureAwait(false);
            await RecalculateOrderFulfillmentStatusAsync(context, orderId).ConfigureAwait(false);
            await AutoAllocatePendingOrdersAsync(context).ConfigureAwait(false);
        }
    }

    public async Task ReserveOrderAsync(LogisticsDbContext context, int orderId)
    {
        var order = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderID == orderId);
        if (order == null || !order.IsPosted || !order.WarehouseID.HasValue) return;

        await AllocateOrderAsync(context, order);
    }

    public async Task ReleaseOrderReserveAsync(LogisticsDbContext context, int orderId)
    {
        await ReleaseOrderAllocationAsync(context, orderId);
    }

    private async Task AutoAllocatePendingOrdersAsync(LogisticsDbContext context)
    {
        var pendingOrders = await context.Orders
            .Where(o => o.FulfillmentStatus != OrderFulfillmentStatus.FullyAllocated && o.IsPosted && o.Status != "Cancelled" && o.Status != "Delivered")
            .OrderByDescending(o => o.Priority)
            .ThenBy(o => o.OrderDate)
            .ToListAsync().ConfigureAwait(false);

        foreach (var order in pendingOrders)
        {
            await AllocateOrderAsync(context, order).ConfigureAwait(false);
        }
    }

    private async Task RecalculateOrderFulfillmentStatusAsync(LogisticsDbContext context, int orderId)
    {
        var order = await context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderID == orderId).ConfigureAwait(false);
        if (order == null || !order.Items.Any()) return;

        int fullyCoveredItems = 0;
        int partiallyCoveredItems = 0;

        foreach (var item in order.Items)
        {
            var reserves = await context.InventoryTransactions
                .Where(t => t.OrderID == orderId && t.ProductID == item.ProductID && t.IsReserve)
                .SumAsync(t => t.Quantity).ConfigureAwait(false);

            double absReserved = Math.Abs(reserves);

            if (absReserved >= item.Quantity)
            {
                fullyCoveredItems++;
            }
            else if (absReserved > 0)
            {
                partiallyCoveredItems++;
            }
        }

        if (fullyCoveredItems == order.Items.Count)
        {
            order.FulfillmentStatus = OrderFulfillmentStatus.FullyAllocated;
        }
        else if (fullyCoveredItems > 0 || partiallyCoveredItems > 0)
        {
            order.FulfillmentStatus = OrderFulfillmentStatus.PartiallyAllocated;
        }
        else
        {
            order.FulfillmentStatus = OrderFulfillmentStatus.NotAllocated;
        }

        context.Orders.Update(order);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task WriteOffWaybillAsync(LogisticsDbContext context, int waybillId)
    {
        var waybill = await context.Waybills.Include(w => w.Points).FirstOrDefaultAsync(w => w.WaybillID == waybillId).ConfigureAwait(false);
        if (waybill == null) return;

        foreach (var point in waybill.Points)
        {
            var orderItems = await context.OrderItems.Where(i => i.OrderID == point.OrderID).ToListAsync().ConfigureAwait(false);
            foreach (var item in orderItems)
            {
                var reserves = await context.InventoryTransactions
                    .Where(t => t.OrderID == point.OrderID && t.ProductID == item.ProductID && t.IsReserve)
                    .ToListAsync().ConfigureAwait(false);

                double totalReserved = Math.Abs(reserves.Sum(t => t.Quantity));
                double qtyToWriteOff = item.Quantity;

                if (point.Status == WaybillPointStatus.PartiallyDelivered && point.DeliveredWeightKG.HasValue)
                {
                    var order = await context.Orders.FindAsync(point.OrderID).ConfigureAwait(false);
                    if (order != null && order.WeightKG > 0)
                    {
                        double ratio = point.DeliveredWeightKG.Value / order.WeightKG;
                        qtyToWriteOff = item.Quantity * ratio;
                    }
                }

                if (totalReserved > 0)
                {
                    context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        Timestamp = DateTime.Now,
                        ProductID = item.ProductID,
                        WarehouseID = 1,
                        Quantity = (int)Math.Round(totalReserved),
                        IsReserve = true,
                        OrderID = point.OrderID,
                        WaybillID = waybillId,
                        SourceDocument = "WaybillWriteOff_ReserveRelease",
                        SourceDocumentID = waybillId
                    });
                }

                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    Timestamp = DateTime.Now,
                    ProductID = item.ProductID,
                    WarehouseID = 1,
                    Quantity = (int)-Math.Round(qtyToWriteOff),
                    IsReserve = false,
                    OrderID = point.OrderID,
                    WaybillID = waybillId,
                    SourceDocument = "WaybillWriteOff_Actual",
                    SourceDocumentID = waybillId
                });
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RevertWaybillWriteOffAsync(LogisticsDbContext context, int waybillId)
    {
        var waybillTransactions = await context.InventoryTransactions
            .Where(t => t.WaybillID == waybillId)
            .ToListAsync().ConfigureAwait(false);

        if (!waybillTransactions.Any()) return;

        foreach (var tx in waybillTransactions)
        {
            context.InventoryTransactions.Add(new InventoryTransaction
            {
                Timestamp = DateTime.Now,
                ProductID = tx.ProductID,
                WarehouseID = tx.WarehouseID,
                Quantity = -tx.Quantity,
                IsReserve = tx.IsReserve,
                OrderID = tx.OrderID,
                WaybillID = null,
                SourceDocument = "WaybillWriteOff_Revert",
                SourceDocumentID = waybillId
            });
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        var orderIds = waybillTransactions.Where(t => t.OrderID.HasValue).Select(t => t.OrderID!.Value).Distinct().ToList();
        foreach (var oid in orderIds)
        {
            var order = await context.Orders.FindAsync(oid).ConfigureAwait(false);
            if (order != null && order.IsPosted && order.Status != "Cancelled")
            {
                await AllocateOrderAsync(context, order).ConfigureAwait(false);
            }
        }
    }
}
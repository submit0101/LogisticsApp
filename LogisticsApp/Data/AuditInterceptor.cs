using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Models;
using LogisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LogisticsApp.Data;

public class AuditEntry
{
    public EntityEntry Entry { get; }
    public string TableName { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object?> KeyValues { get; } = new();
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();

    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
    }

    public AuditLog ToAuditLog()
    {
        var keyStr = KeyValues.Any()
            ? string.Join(", ", KeyValues.Select(kv => $"{kv.Key}={kv.Value}"))
            : Entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? string.Empty;

        return new AuditLog
        {
            RecordID = keyStr,
            EntityName = TableName,
            Action = Action,
            OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
            NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues),
            Timestamp = DateTime.Now,
            UserID = UserId,
            Details = $"Системная запись аудита для: {TableName}"
        };
    }
}

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly SecurityService _securityService;

    public AuditInterceptor(SecurityService securityService)
    {
        _securityService = securityService;
    }

    private string TranslateState(EntityState state) => state switch
    {
        EntityState.Added => "Создание",
        EntityState.Modified => "Изменение",
        EntityState.Deleted => "Удаление",
        _ => state.ToString()
    };

    private string TranslateTableName(string tableName) => tableName switch
    {
        "Users" => "Пользователи",
        "Vehicles" => "Автопарк",
        "Drivers" => "Водители",
        "Customers" => "Контрагенты",
        "Orders" => "Заказы покупателей",
        "OrderItems" => "Состав заказа",
        "Waybills" => "Путевые листы",
        "WaybillPoints" => "Точки маршрута",
        "VehicleServiceRecords" => "Заказ-наряды (ТО)",
        "Products" => "Номенклатура",
        "ProductGroups" => "Группы номенклатуры",
        _ => tableName
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var userId = _securityService.CurrentUser?.UserID;

        var entries = eventData.Context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog && e.Entity is not LogEntry && e.Entity is not OutboxMessage && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (!entries.Any()) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var outboxMessages = new List<OutboxMessage>();

        foreach (var entry in entries)
        {
            var rawTableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
            var auditEntry = new AuditEntry(entry)
            {
                TableName = TranslateTableName(rawTableName),
                UserId = userId,
                Action = TranslateState(entry.State)
            };

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }

            var auditLog = auditEntry.ToAuditLog();

            outboxMessages.Add(new OutboxMessage
            {
                Type = "AuditLog",
                Payload = JsonSerializer.Serialize(auditLog)
            });
        }

        eventData.Context.Set<OutboxMessage>().AddRange(outboxMessages);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
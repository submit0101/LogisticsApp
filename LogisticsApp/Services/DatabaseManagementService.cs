using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Services;

public sealed class DatabaseManagementService : IDatabaseManagementService
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;

    public DatabaseManagementService(IDbContextFactory<LogisticsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task BackupDatabaseAsync(string targetFilePath, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var dbName = context.Database.GetDbConnection().Database;
        var sql = $"BACKUP DATABASE [{dbName}] TO DISK = '{targetFilePath}' WITH FORMAT, MEDIANAME = 'LogisticsApp_Backup', NAME = 'Full Backup'";
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    public async Task WipeDatabaseAsync()
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

            var tablesToPreserve = new[] { "Users", "Roles", "Permissions", "RolePermissions", "Units", "Warehouses", "__EFMigrationsHistory" };
            var tableNames = context.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(t => t != null && !tablesToPreserve.Contains(t)).ToList();

            foreach (var table in tableNames)
            {
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM [{table}]");

                var checkIdentSql = $@"
                    IF OBJECTPROPERTY(OBJECT_ID('[{table}]'), 'TableHasIdentity') = 1
                    BEGIN
                        DBCC CHECKIDENT ('[{table}]', RESEED, 0)
                    END";
                await context.Database.ExecuteSqlRawAsync(checkIdentSql);
            }

            await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task GenerateTestDataAsync(int count, string targetDictionary = "All")
    {
        using var context = await _dbFactory.CreateDbContextAsync();
        var random = new Random();

        if (targetDictionary == "All" || targetDictionary == "Customers")
        {
            var firstNames = new[] { "Иван", "Алексей", "Дмитрий", "Сергей", "Михаил", "Анна", "Екатерина", "Ольга", "Андрей", "Максим" };
            var lastNames = new[] { "Иванов", "Смирнов", "Кузнецов", "Попов", "Васильев", "Петров", "Соколов", "Михайлов", "Новиков", "Филиппов" };
            var companies = new[] { "Логистика Плюс", "Транс-Сервис", "ГрузАвто", "МегаТранс", "Регион-Доставка", "СтройОпт", "ПромСнаб", "АгроТрейд", "ТехПром", "Вектор" };

            var customers = new List<Customer>();
            for (int i = 0; i < count; i++)
            {
                bool isLegal = random.Next(2) == 0;
                string name = isLegal ? $"ООО \"{companies[random.Next(companies.Length)]}\"" : $"ИП {lastNames[random.Next(lastNames.Length)]} {firstNames[random.Next(firstNames.Length)][0]}.";

                string inn = isLegal
                    ? random.NextInt64(1000000000L, 9999999999L).ToString()
                    : random.NextInt64(100000000000L, 999999999999L).ToString();

                customers.Add(new Customer
                {
                    Name = name,
                    FullName = name,
                    INN = inn,
                    Type = isLegal ? CustomerType.LegalEntity : CustomerType.Entrepreneur,
                    Address = $"г. Москва, ул. Примерная, д. {random.Next(1, 150)}"
                });
            }
            context.Customers.AddRange(customers);
        }

        if (targetDictionary == "All" || targetDictionary == "Vehicles")
        {
            var vehicleBrands = new[] { "LADA", "ГАЗ", "КАМАЗ", "Mercedes-Benz", "Volvo", "Scania", "Ford", "MAN", "DAF", "Renault" };
            var vehicleModels = new[] { "Largus", "ГАЗель NEXT", "5490 NEO", "Actros", "FH16", "R450", "Transit", "TGX", "XF", "Magnum" };
            var regions = new[] { "77", "99", "97", "177", "199", "197", "799", "797", "50", "90", "150", "190", "750", "78", "98", "178" };
            var letters = new[] { "А", "В", "Е", "К", "М", "Н", "О", "Р", "С", "Т", "У", "Х" };

            var vehicles = new List<Vehicle>();
            for (int i = 0; i < count; i++)
            {
                string reg = $"{letters[random.Next(letters.Length)]}{random.Next(100, 999)}{letters[random.Next(letters.Length)]}{letters[random.Next(letters.Length)]}{regions[random.Next(regions.Length)]}";
                string brand = vehicleBrands[random.Next(vehicleBrands.Length)];
                string model = vehicleModels[random.Next(vehicleModels.Length)];

                vehicles.Add(new Vehicle
                {
                    RegNumber = reg,
                    Model = $"{brand} {model}",
                    VIN = "XTA" + random.Next(100000, 999999).ToString() + random.Next(10000000, 99999999).ToString(),
                    Year = random.Next(2010, DateTime.Today.Year),
                    CapacityKG = random.Next(1500, 20000),
                    CapacityM3 = random.Next(10, 120),
                    Mileage = random.Next(1000, 500000),
                    IsFridge = random.Next(2) == 0,
                    Status = VehicleStatus.Active,
                    FuelType = FuelType.DT,
                    SanitizationDate = DateTime.Today.AddDays(-random.Next(1, 60)),
                    BaseFuelConsumption = random.Next(10, 35) + Math.Round(random.NextDouble(), 1),
                    CurrentFuelLevel = random.Next(10, 200),
                    FuelCapacity = random.Next(100, 800),
                    CargoFuelBonus = 1.5,
                    WinterFuelBonus = 10.0
                });
            }
            context.Vehicles.AddRange(vehicles);
        }

        if (targetDictionary == "All" || targetDictionary == "Nomenclature")
        {
            var productGroups = new[] { "ГСМ", "Автозапчасти", "Шины", "Аккумуляторы", "Спецодежда", "Инструменты" };
            var groups = new List<ProductGroup>();
            foreach (var g in productGroups)
            {
                var group = await context.ProductGroups.FirstOrDefaultAsync(x => x.Name == g);
                if (group == null)
                {
                    group = new ProductGroup { Name = g };
                    context.ProductGroups.Add(group);
                }
                groups.Add(group);
            }
            await context.SaveChangesAsync();

            var baseUnit = await context.Units.FirstOrDefaultAsync(u => u.Name == "шт") ?? new Unit { Name = "шт", FullName = "Штука", Code = "796" };
            if (baseUnit.UnitID == 0) context.Units.Add(baseUnit);
            await context.SaveChangesAsync();

            var products = new List<Product>();
            for (int i = 0; i < count; i++)
            {
                var group = groups[random.Next(groups.Count)];
                products.Add(new Product
                {
                    SKU = $"PRD-{random.Next(100000, 999999)}",
                    Name = $"{group.Name} Арт.{random.Next(1, 9999)}",
                    GroupID = group.GroupID,
                    BaseUnitID = baseUnit.UnitID,
                    Barcode = $"460{random.Next(100000000, 999999999)}"
                });
            }
            context.Products.AddRange(products);
        }

        await context.SaveChangesAsync();
    }
}
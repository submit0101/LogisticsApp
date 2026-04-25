using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(LogisticsDbContext context)
    {
        // Создаем базу, если её нет. 
        // Важно: EnsureCreatedAsync не применяет миграции к существующей базе.
        await context.Database.EnsureCreatedAsync();

        // Разбиваем инициализацию на независимые этапы
        await SeedUsersAsync(context);
        await SeedUnitsAsync(context);
        await SeedWarehousesAsync(context);
        await SeedTestDataAsync(context);
    }

    private static async Task SeedUsersAsync(LogisticsDbContext context)
    {
        if (await context.Users.AnyAsync()) return;

        var authService = App.AppHost!.Services.GetRequiredService<IAuthService>();

        var defaultAdmin = new User
        {
            Login = "admin",
            FullName = "Системный Администратор",
            // Роли удалены из модели User
            PasswordHash = authService.HashPassword("admin123")
        };

        context.Users.Add(defaultAdmin);
        await context.SaveChangesAsync();
    }

    private static async Task SeedUnitsAsync(LogisticsDbContext context)
    {
        if (await context.Units.AnyAsync()) return;

        var defaultUnits = new List<Unit>
        {
            new Unit { Name = "шт", FullName = "Штука", Code = "796" },
            new Unit { Name = "кг", FullName = "Килограмм", Code = "166" },
            new Unit { Name = "л", FullName = "Литр", Code = "112" },
            new Unit { Name = "м³", FullName = "Кубический метр", Code = "113" }
        };

        context.Units.AddRange(defaultUnits);
        await context.SaveChangesAsync();
    }

    private static async Task SeedWarehousesAsync(LogisticsDbContext context)
    {
        if (await context.Warehouses.AnyAsync()) return;

        context.Warehouses.Add(new Warehouse
        {
            Name = "Основной склад готовой продукции",
            Address = "Территория предприятия",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedTestDataAsync(LogisticsDbContext context)
    {
        // Контрагенты
        if (!await context.Customers.AnyAsync())
        {
            context.Customers.Add(new Customer
            {
                Type = CustomerType.LegalEntity,
                INN = "7701234567",
                Name = "ООО «Тестовый Контрагент»",
                FullName = "Общество с ограниченной ответственностью «Тестовый Контрагент»",
                Phone = "+7 (999) 123-45-67"
            });
        }

        // Автопарк
        if (!await context.Vehicles.AnyAsync())
        {
            context.Vehicles.Add(new Vehicle
            {
                RegNumber = "А123АА77",
                Model = "ГАЗель NEXT",
                CapacityKG = 1500,
                Status = VehicleStatus.Active
            });
        }

        // Номенклатура
        if (!await context.Products.AnyAsync())
        {
            var unitPcs = await context.Units.FirstOrDefaultAsync(u => u.Name == "шт") ?? await context.Units.FirstAsync();
            context.Products.Add(new Product
            {
                SKU = "TEST-001",
                Name = "Тестовый мясной деликатес",
                BaseUnitID = unitPcs.UnitID
            });
        }

        await context.SaveChangesAsync();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using LogisticsApp.Services;
using LogisticsApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogisticsApp.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(LogisticsDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
        await EnsureRbacSchemaUpgradedAsync(context);

        var existingPerms = await context.Permissions.Select(p => p.SystemName).ToListAsync();

        var allPermissions = new List<Permission>
        {
            new Permission { SystemName = "AccessSettings", DisplayName = "Доступ к настройкам", Module = "Система" },
            new Permission { SystemName = "ManageUsers", DisplayName = "Управление пользователями", Module = "Система" },
            new Permission { SystemName = "ViewAuditLog", DisplayName = "Просмотр аудита и логов", Module = "Система" },
            new Permission { SystemName = "ViewVehicles", DisplayName = "Просмотр автопарка", Module = "Автопарк" },
            new Permission { SystemName = "EditVehicles", DisplayName = "Добавление/Редактирование", Module = "Автопарк" },
            new Permission { SystemName = "DeleteVehicles", DisplayName = "Удаление", Module = "Автопарк" },
            new Permission { SystemName = "ViewDrivers", DisplayName = "Просмотр водителей", Module = "Сотрудники" },
            new Permission { SystemName = "EditDrivers", DisplayName = "Добавление/Редактирование", Module = "Сотрудники" },
            new Permission { SystemName = "DeleteDrivers", DisplayName = "Удаление", Module = "Сотрудники" },
            new Permission { SystemName = "ViewCustomers", DisplayName = "Просмотр контрагентов", Module = "Справочники" },
            new Permission { SystemName = "EditCustomers", DisplayName = "Добавление/Редактирование", Module = "Справочники" },
            new Permission { SystemName = "DeleteCustomers", DisplayName = "Удаление", Module = "Справочники" },
            new Permission { SystemName = "ViewOrders", DisplayName = "Просмотр заказов", Module = "Операции" },
            new Permission { SystemName = "EditOrders", DisplayName = "Оформление заказов", Module = "Операции" },
            new Permission { SystemName = "DeleteOrders", DisplayName = "Удаление/Отмена", Module = "Операции" },
            new Permission { SystemName = "ViewWaybills", DisplayName = "Просмотр путевых листов", Module = "Операции" },
            new Permission { SystemName = "EditWaybills", DisplayName = "Диспетчеризация и выпуск", Module = "Операции" },
            new Permission { SystemName = "DeleteWaybills", DisplayName = "Удаление", Module = "Операции" },
            new Permission { SystemName = "ViewReports", DisplayName = "Доступ к аналитике", Module = "Отчеты" },
            new Permission { SystemName = "ExportReports", DisplayName = "Экспорт данных", Module = "Отчеты" },
            new Permission { SystemName = "ViewNomenclature", DisplayName = "Просмотр номенклатуры", Module = "Справочники" },
            new Permission { SystemName = "EditNomenclature", DisplayName = "Редактирование номенклатуры", Module = "Справочники" },
            new Permission { SystemName = "DeleteNomenclature", DisplayName = "Удаление номенклатуры", Module = "Справочники" },
            new Permission { SystemName = "ViewInventory", DisplayName = "Просмотр склада и запасов", Module = "Склад" },
            new Permission { SystemName = "EditInventory", DisplayName = "Оформление складских документов", Module = "Склад" },
            new Permission { SystemName = "ViewFinance", DisplayName = "Просмотр взаиморасчетов", Module = "Казначейство" },
            new Permission { SystemName = "EditFinance", DisplayName = "Проведение платежей (ПКО/РКО)", Module = "Казначейство" }
        };

        var permsToAdd = allPermissions.Where(p => !existingPerms.Contains(p.SystemName)).ToList();

        if (permsToAdd.Any())
        {
            context.Permissions.AddRange(permsToAdd);
            await context.SaveChangesAsync();

            var adminRoleExisting = await context.Roles.FirstOrDefaultAsync(r => r.IsSystem && r.Name == "Администратор");
            if (adminRoleExisting != null)
            {
                foreach (var perm in permsToAdd)
                {
                    context.RolePermissions.Add(new RolePermission { RoleID = adminRoleExisting.RoleID, PermissionID = perm.PermissionID });
                }
                await context.SaveChangesAsync();
            }
        }

        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { Name = "Администратор", Description = "Полный доступ к системе (IT-служба)", IsSystem = true },
                new Role { Name = "Директор", Description = "Просмотр всей информации, аналитики и аудита", IsSystem = false },
                new Role { Name = "Логист", Description = "Планирование маршрутов и путевых листов", IsSystem = false },
                new Role { Name = "Диспетчер АТП", Description = "Управление автопарком и водителями", IsSystem = false },
                new Role { Name = "Менеджер продаж", Description = "Работа с заказами и клиентами", IsSystem = false },
                new Role { Name = "Кладовщик", Description = "Управление товарными запасами (WMS)", IsSystem = false },
                new Role { Name = "Бухгалтер", Description = "Взаиморасчеты, казначейство и отчетность", IsSystem = false }
            };

            context.Roles.AddRange(roles);
            await context.SaveChangesAsync();

            var dbPerms = await context.Permissions.ToDictionaryAsync(p => p.SystemName, p => p.PermissionID);

            void AssignPermissions(string roleName, params string[] permKeys)
            {
                var role = roles.First(r => r.Name == roleName);
                foreach (var key in permKeys)
                {
                    if (dbPerms.TryGetValue(key, out int permId))
                    {
                        context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionID = permId });
                    }
                }
            }

            AssignPermissions("Администратор", dbPerms.Keys.ToArray());
            AssignPermissions("Директор",
                "ViewAuditLog", "ViewVehicles", "ViewDrivers", "ViewCustomers", "ViewOrders",
                "ViewWaybills", "ViewReports", "ExportReports", "ViewNomenclature", "ViewInventory", "ViewFinance");
            AssignPermissions("Логист",
                "ViewVehicles", "ViewDrivers", "ViewCustomers", "ViewOrders",
                "ViewWaybills", "EditWaybills", "ViewReports", "ExportReports",
                "ViewNomenclature", "ViewInventory");
            AssignPermissions("Диспетчер АТП",
                "ViewVehicles", "EditVehicles", "DeleteVehicles", "ViewDrivers", "EditDrivers", "DeleteDrivers", "ViewWaybills");
            AssignPermissions("Менеджер продаж",
                "ViewCustomers", "EditCustomers", "ViewOrders", "EditOrders", "DeleteOrders",
                "ViewNomenclature", "ViewInventory", "ViewFinance");
            AssignPermissions("Кладовщик",
                "ViewNomenclature", "ViewInventory", "EditInventory", "ViewOrders");
            AssignPermissions("Бухгалтер",
                "ViewCustomers", "EditCustomers", "ViewOrders", "ViewWaybills",
                "ViewReports", "ExportReports", "ViewNomenclature", "ViewFinance", "EditFinance");

            await context.SaveChangesAsync();
        }

        if (!await context.Users.AnyAsync())
        {
            var adminRole = await context.Roles.FirstAsync(r => r.IsSystem && r.Name == "Администратор");
            var authService = App.AppHost!.Services.GetRequiredService<IAuthService>();

            var defaultAdmin = new User
            {
                Login = "admin",
                FullName = "Системный Администратор",
                RoleID = adminRole.RoleID,
                PasswordHash = authService.HashPassword("admin123")
            };

            context.Users.Add(defaultAdmin);
            await context.SaveChangesAsync();
        }

        if (!await context.Units.AnyAsync())
        {
            var defaultUnits = new List<Unit>
            {
                new Unit { Name = "шт", FullName = "Штука", Code = "796" },
                new Unit { Name = "кг", FullName = "Килограмм", Code = "166" },
                new Unit { Name = "г", FullName = "Грамм", Code = "163" },
                new Unit { Name = "т", FullName = "Тонна", Code = "168" },
                new Unit { Name = "л", FullName = "Литр", Code = "112" },
                new Unit { Name = "м³", FullName = "Кубический метр", Code = "113" },
                new Unit { Name = "упак", FullName = "Упаковка", Code = "778" },
                new Unit { Name = "кор", FullName = "Коробка", Code = "728" },
                new Unit { Name = "палл", FullName = "Паллета", Code = "734" }
            };

            context.Units.AddRange(defaultUnits);
            await context.SaveChangesAsync();
        }

        if (!await context.Warehouses.AnyAsync())
        {
            context.Warehouses.Add(new Warehouse { Name = "Основной склад готовой продукции", Address = "Территория предприятия", IsActive = true });
            await context.SaveChangesAsync();
        }

        if (!await context.Customers.AnyAsync())
        {
            context.Customers.Add(new Customer
            {
                Type = CustomerType.LegalEntity,
                INN = "7701234567",
                KPP = "770101001",
                OGRN = "1027700132195",
                Name = "ООО «Тестовый Контрагент»",
                FullName = "Общество с ограниченной ответственностью «Тестовый Контрагент»",
                LegalAddress = "г. Москва, ул. Тестовая, д. 1",
                Address = "г. Москва, ул. Доставочная, д. 2",
                ContactPerson = "Иванов Иван",
                Phone = "+7 (999) 123-45-67",
                Email = "test@example.com"
            });
            await context.SaveChangesAsync();
        }

        if (!await context.Vehicles.AnyAsync())
        {
            context.Vehicles.Add(new Vehicle
            {
                RegNumber = "А123АА77",
                Model = "ГАЗель NEXT",
                VIN = "X96330200L0000000",
                Year = DateTime.Today.Year - 3,
                CapacityKG = 1500,
                CapacityM3 = 10.0,
                Mileage = 45000,
                IsFridge = true,
                SanitizationDate = DateTime.Today.AddDays(-5),
                Status = VehicleStatus.Active,
                FuelType = FuelType.DT,
                CurrentFuelLevel = 45.5,
                BaseFuelConsumption = 12.0,
                CargoFuelBonus = 1.5,
                WinterFuelBonus = 10.0
            });
            await context.SaveChangesAsync();
        }

        if (!await context.Drivers.AnyAsync())
        {
            context.Drivers.Add(new Driver
            {
                LastName = "Смирнов",
                FirstName = "Алексей",
                MiddleName = "Иванович",
                LicenseNumber = "77 АА 123456",
                LicenseCategories = "B, C",
                LicenseExpirationDate = DateTime.Today.AddYears(5),
                Phone = "+7 (900) 555-33-22",
                EmploymentDate = DateTime.Today.AddYears(-1),
                Status = DriverStatus.Active,
                MedicalCertificateNumber = "003-В/у № 112233",
                MedicalCertificateExpiration = DateTime.Today.AddYears(1)
            });
            await context.SaveChangesAsync();
        }

        if (!await context.Products.AnyAsync())
        {
            var unitPcs = await context.Units.FirstOrDefaultAsync(u => u.Name == "шт") ?? await context.Units.FirstAsync();
            var testProduct = new Product
            {
                SKU = "TEST-001",
                Name = "Тестовый товар (Мясной деликатес)",
                ShelfLife = "14 суток",
                StorageConditions = "от +2 до +6 С",
                Barcode = "4601234567890",
                BaseUnitID = unitPcs.UnitID
            };
            testProduct.Prices.Add(new ProductPrice { Period = DateTime.Today.AddDays(-10), Value = 1500.00m });
            context.Products.Add(testProduct);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureRbacSchemaUpgradedAsync(LogisticsDbContext context)
    {
        var sql = @"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
        BEGIN
            CREATE TABLE [Roles] (
                [RoleID] int NOT NULL IDENTITY,
                [Name] nvarchar(50) NOT NULL,
                [Description] nvarchar(200) NULL,
                [IsSystem] bit NOT NULL,
                CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleID])
            );
            CREATE TABLE [Permissions] (
                [PermissionID] int NOT NULL IDENTITY,
                [SystemName] nvarchar(100) NOT NULL,
                [DisplayName] nvarchar(100) NOT NULL,
                [Module] nvarchar(100) NOT NULL,
                CONSTRAINT [PK_Permissions] PRIMARY KEY ([PermissionID])
            );
            CREATE TABLE [RolePermissions] (
                [RoleID] int NOT NULL,
                [PermissionID] int NOT NULL,
                CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleID], [PermissionID]),
                CONSTRAINT [FK_RolePermissions_Permissions_PermissionID] FOREIGN KEY ([PermissionID]) REFERENCES [Permissions] ([PermissionID]) ON DELETE CASCADE,
                CONSTRAINT [FK_RolePermissions_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID]) ON DELETE CASCADE
            );
            IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'RoleID' AND Object_ID = Object_ID(N'Users'))
            BEGIN
                ALTER TABLE [Users] ADD [RoleID] int NULL;
            END
            INSERT INTO [Roles] ([Name], [Description], [IsSystem]) 
            VALUES ('Администратор', 'Создана автоматически', 1);
            DECLARE @adminRoleId int = SCOPE_IDENTITY();
            UPDATE [Users] SET [RoleID] = @adminRoleId WHERE [RoleID] IS NULL;
            ALTER TABLE [Users] ALTER COLUMN [RoleID] int NOT NULL;
            ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID]) ON DELETE CASCADE;
            IF EXISTS(SELECT * FROM sys.columns WHERE Name = N'Role' AND Object_ID = Object_ID(N'Users'))
            BEGIN
                DECLARE @ConstraintName nvarchar(200)
                SELECT @ConstraintName = Name FROM sys.default_constraints 
                WHERE PARENT_OBJECT_ID = OBJECT_ID('Users') AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = N'Role' AND object_id = OBJECT_ID(N'Users'))
                
                IF @ConstraintName IS NOT NULL
                    EXEC('ALTER TABLE [Users] DROP CONSTRAINT ' + @ConstraintName)
                ALTER TABLE [Users] DROP COLUMN [Role];
            END
        END";
        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Data;

public class LogisticsDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Waybill> Waybills { get; set; }
    public DbSet<WaybillPoint> WaybillPoints { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<VehicleServiceRecord> VehicleServiceRecords { get; set; }
    public DbSet<ProductGroup> ProductGroups { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductPrice> ProductPrices { get; set; }
    public DbSet<ProductPackaging> ProductPackagings { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<MutualSettlement> MutualSettlements { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<InventoryDocument> InventoryDocuments { get; set; }
    public DbSet<InventoryDocumentItem> InventoryDocumentItems { get; set; }
    public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
    public DbSet<FuelTicket> FuelTickets { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public LogisticsDbContext(DbContextOptions<LogisticsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configurations.DriverConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.VehicleConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.VehicleServiceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.WaybillConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.WaybillPointConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ProductGroupConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ProductConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ProductPriceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ProductPackagingConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.UnitConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.MutualSettlementConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.WarehouseConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.InventoryDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.InventoryDocumentItemConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.InventoryTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.FuelTicketConfiguration());

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).HasMaxLength(255).IsRequired();
            entity.Property(m => m.Payload).IsRequired();
            entity.HasIndex(m => m.ProcessedAt);
            entity.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<Customer>().Property(c => c.Type).HasConversion<string>();

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Warehouse)
            .WithMany()
            .HasForeignKey(o => o.WarehouseID)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Driver>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Vehicle>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Waybill>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<WaybillPoint>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Unit>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<InventoryDocument>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        ApplySoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.Now;
            }
        }
    }
}
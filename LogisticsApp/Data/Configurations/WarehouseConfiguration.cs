using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(w => w.WarehouseID);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(150);
        builder.Property(w => w.Address).HasMaxLength(500);
        builder.Property(w => w.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}
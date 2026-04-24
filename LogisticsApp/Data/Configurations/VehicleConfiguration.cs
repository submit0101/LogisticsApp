using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.VehicleID);
        builder.Property(v => v.RegNumber).IsRequired().HasMaxLength(20);
        builder.Property(v => v.Model).IsRequired().HasMaxLength(100);
        builder.Property(v => v.VIN).HasMaxLength(17);
        builder.Property(v => v.Status).HasConversion<string>();
        builder.Property(v => v.FuelType).HasConversion<string>();
        builder.Property(v => v.FuelCapacity).HasColumnType("float").HasDefaultValue(100.0);
        builder.Property(v => v.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}
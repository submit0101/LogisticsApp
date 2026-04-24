using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class WaybillConfiguration : IEntityTypeConfiguration<Waybill>
{
    public void Configure(EntityTypeBuilder<Waybill> builder)
    {
        builder.ToTable("Waybills");
        builder.HasKey(w => w.WaybillID);

        builder.Property(w => w.Notes).HasMaxLength(1000);
        builder.Property(w => w.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.DateCreate);

        builder.HasOne(w => w.Vehicle)
               .WithMany()
               .HasForeignKey(w => w.VehicleID)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Driver)
               .WithMany()
               .HasForeignKey(w => w.DriverID)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Points)
               .WithOne(p => p.Waybill)
               .HasForeignKey(p => p.WaybillID)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
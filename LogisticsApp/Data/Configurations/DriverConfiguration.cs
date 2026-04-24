using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");
        builder.HasKey(d => d.DriverID);

        builder.Property(d => d.RowVersion)
               .IsRowVersion()
               .IsConcurrencyToken();

        builder.HasIndex(d => d.LastName);
        builder.HasIndex(d => d.LicenseNumber).IsUnique();
        builder.HasIndex(d => d.Status);

        builder.HasMany(d => d.Waybills)
               .WithOne(w => w.Driver)
               .HasForeignKey(w => w.DriverID)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
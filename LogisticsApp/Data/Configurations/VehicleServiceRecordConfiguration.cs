using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class VehicleServiceRecordConfiguration : IEntityTypeConfiguration<VehicleServiceRecord>
{
    public void Configure(EntityTypeBuilder<VehicleServiceRecord> builder)
    {
        builder.ToTable("VehicleServiceRecords");
        builder.HasKey(r => r.RecordID);
        builder.Property(r => r.Description).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.MechanicName).HasMaxLength(150);
        builder.Property(r => r.Cost).HasColumnType("decimal(18,2)");
        builder.Property(r => r.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(r => r.ServiceDate);
        builder.HasIndex(r => r.ServiceType);
    }
}
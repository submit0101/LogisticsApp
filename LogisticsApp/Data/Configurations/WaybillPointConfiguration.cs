using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class WaybillPointConfiguration : IEntityTypeConfiguration<WaybillPoint>
{
    public void Configure(EntityTypeBuilder<WaybillPoint> builder)
    {
        builder.ToTable("WaybillPoints");
        builder.HasKey(p => p.WP_ID);

        builder.Property(p => p.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(p => p.Order)
               .WithMany()
               .HasForeignKey(p => p.OrderID)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
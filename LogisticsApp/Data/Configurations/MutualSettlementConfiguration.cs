using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class MutualSettlementConfiguration : IEntityTypeConfiguration<MutualSettlement>
{
    public void Configure(EntityTypeBuilder<MutualSettlement> builder)
    {
        builder.ToTable("MutualSettlements");
        builder.HasKey(m => m.SettlementID);
        builder.Property(m => m.Amount).HasColumnType("decimal(18,2)");
        builder.HasOne(m => m.Customer).WithMany().HasForeignKey(m => m.CustomerID).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Order).WithMany().HasForeignKey(m => m.OrderID).OnDelete(DeleteBehavior.SetNull);
    }
}
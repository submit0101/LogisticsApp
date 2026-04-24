using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class FuelTicketConfiguration : IEntityTypeConfiguration<FuelTicket>
{
    public void Configure(EntityTypeBuilder<FuelTicket> builder)
    {
        builder.ToTable("FuelTickets");
        builder.HasKey(t => t.TicketID);
        builder.Property(t => t.Amount).HasColumnType("decimal(18,2)");
        builder.Property(t => t.PricePerLiter).HasColumnType("decimal(18,2)");
        builder.Property(t => t.TicketNumber).HasMaxLength(50);
        builder.Property(t => t.FuelType).HasConversion<string>();
        builder.HasOne(t => t.Waybill).WithMany(w => w.FuelTickets).HasForeignKey(t => t.WaybillID).OnDelete(DeleteBehavior.Cascade);
    }
}
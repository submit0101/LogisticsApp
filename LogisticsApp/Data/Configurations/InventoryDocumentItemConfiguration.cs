using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class InventoryDocumentItemConfiguration : IEntityTypeConfiguration<InventoryDocumentItem>
{
    public void Configure(EntityTypeBuilder<InventoryDocumentItem> builder)
    {
        builder.ToTable("InventoryDocumentItems");
        builder.HasKey(i => i.ItemID);
        builder.Property(i => i.CostPrice).HasColumnType("decimal(18,2)");
        builder.HasOne(i => i.Document).WithMany(d => d.Items).HasForeignKey(i => i.DocumentID).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductID).OnDelete(DeleteBehavior.Restrict);
    }
}
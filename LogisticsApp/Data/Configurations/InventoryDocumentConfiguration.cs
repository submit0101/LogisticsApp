using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
{
    public void Configure(EntityTypeBuilder<InventoryDocument> builder)
    {
        builder.ToTable("InventoryDocuments");
        builder.HasKey(d => d.DocumentID);
        builder.Property(d => d.Reason).HasMaxLength(1000);
        builder.Property(d => d.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(d => d.Warehouse).WithMany(w => w.Documents).HasForeignKey(d => d.WarehouseID).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Order).WithMany().HasForeignKey(d => d.OrderID).OnDelete(DeleteBehavior.SetNull);
    }
}
using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");
        builder.HasKey(t => t.TransactionID);
        builder.HasIndex(t => new { t.ProductID, t.WarehouseID });
        builder.HasIndex(t => new { t.SourceDocument, t.SourceDocumentID });
        builder.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductID).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Warehouse).WithMany(w => w.Transactions).HasForeignKey(t => t.WarehouseID).OnDelete(DeleteBehavior.Restrict);
    }
}
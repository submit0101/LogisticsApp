using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.ProductID);
        builder.Property(p => p.SKU).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(255);

        builder.Property(p => p.ShelfLife).HasMaxLength(100);
        builder.Property(p => p.StorageConditions).HasMaxLength(255);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasOne(p => p.Group)
               .WithMany(g => g.Products)
               .HasForeignKey(p => p.GroupID)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
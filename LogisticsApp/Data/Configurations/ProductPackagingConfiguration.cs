using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class ProductPackagingConfiguration : IEntityTypeConfiguration<ProductPackaging>
{
    public void Configure(EntityTypeBuilder<ProductPackaging> builder)
    {
        builder.ToTable("ProductPackagings");
        builder.HasKey(p => p.PackagingID);
        builder.Property(p => p.Coefficient).HasColumnType("decimal(18,4)");
        builder.Property(p => p.Barcode).HasMaxLength(100);

        builder.HasOne(p => p.Product)
               .WithMany(pr => pr.Packagings)
               .HasForeignKey(p => p.ProductID)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Unit)
               .WithMany(u => u.Packagings)
               .HasForeignKey(p => p.UnitID)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
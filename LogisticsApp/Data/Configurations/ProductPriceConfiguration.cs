using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("ProductPrices");
        builder.HasKey(p => p.PriceID);
        builder.Property(p => p.Value).HasColumnType("decimal(18,2)");
        builder.HasOne(p => p.Product).WithMany(p => p.Prices).HasForeignKey(p => p.ProductID).OnDelete(DeleteBehavior.Cascade);
    }
}
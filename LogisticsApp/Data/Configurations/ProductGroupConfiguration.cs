using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.ToTable("ProductGroups");
        builder.HasKey(g => g.GroupID);
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(g => g.ParentGroup)
               .WithMany(g => g.SubGroups)
               .HasForeignKey(g => g.ParentGroupID)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
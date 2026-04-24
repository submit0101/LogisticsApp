using LogisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogisticsApp.Data.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.HasKey(u => u.UnitID);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(50);
        builder.Property(u => u.FullName).HasMaxLength(100);
        builder.Property(u => u.Code).HasMaxLength(10);
    }
}
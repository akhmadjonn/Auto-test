using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class ColorVisionPlateConfiguration : IEntityTypeConfiguration<ColorVisionPlate>
{
    public void Configure(EntityTypeBuilder<ColorVisionPlate> builder)
    {
        builder.ToTable(TableNames.ColorVisionPlates);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.Property(e => e.ExpectedAnswer).HasMaxLength(50);
        builder.Property(e => e.AlternateAnswer).HasMaxLength(50);

        builder.HasIndex(e => e.SortOrder);
    }
}

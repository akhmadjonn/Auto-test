using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class RoadSignConfiguration : IEntityTypeConfiguration<RoadSign>
{
    public void Configure(EntityTypeBuilder<RoadSign> builder)
    {
        builder.ToTable(TableNames.RoadSigns);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.SignCode).HasMaxLength(20);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.Property(e => e.ThumbnailUrl).HasMaxLength(500);

        builder.HasIndex(e => e.SignCode).IsUnique();
        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.CategoryId, e.SortOrder });
    }
}

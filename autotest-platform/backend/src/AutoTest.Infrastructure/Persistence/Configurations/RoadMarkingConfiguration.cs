using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class RoadMarkingConfiguration : IEntityTypeConfiguration<RoadMarking>
{
    public void Configure(EntityTypeBuilder<RoadMarking> builder)
    {
        builder.ToTable(TableNames.RoadMarkings);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.MarkingCode).HasMaxLength(20);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.Property(e => e.ThumbnailUrl).HasMaxLength(500);

        builder.HasIndex(e => e.MarkingCode).IsUnique();
        builder.HasIndex(e => e.MarkingType);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.MarkingType, e.SortOrder });
    }
}

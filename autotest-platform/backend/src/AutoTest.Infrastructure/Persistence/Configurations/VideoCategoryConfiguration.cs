using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class VideoCategoryConfiguration : IEntityTypeConfiguration<VideoCategory>
{
    public void Configure(EntityTypeBuilder<VideoCategory> builder)
    {
        builder.ToTable(TableNames.VideoCategories);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.IconUrl).HasMaxLength(500);

        builder.HasIndex(e => new { e.SortOrder, e.IsActive });

        builder.HasMany(e => e.Lessons)
            .WithOne(l => l.Category)
            .HasForeignKey(l => l.VideoCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

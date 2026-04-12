using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class RoadSignCategoryConfiguration : IEntityTypeConfiguration<RoadSignCategory>
{
    public void Configure(EntityTypeBuilder<RoadSignCategory> builder)
    {
        builder.ToTable(TableNames.RoadSignCategories);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.Slug).HasMaxLength(100);
        builder.Property(e => e.Code).HasMaxLength(10);
        builder.Property(e => e.IconUrl).HasMaxLength(500);

        builder.HasIndex(e => e.Slug).IsUnique();
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.IsActive);

        builder.HasMany(e => e.Signs)
            .WithOne(s => s.Category)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

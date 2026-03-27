using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class GlossaryCategoryConfiguration : IEntityTypeConfiguration<GlossaryCategory>
{
    public void Configure(EntityTypeBuilder<GlossaryCategory> builder)
    {
        builder.ToTable(TableNames.GlossaryCategories);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());

        builder.Property(e => e.Slug).HasMaxLength(100);
        builder.Property(e => e.Icon).HasMaxLength(200);

        builder.HasIndex(e => e.Slug).IsUnique();

        builder.HasMany(e => e.Terms)
            .WithOne(t => t.Category)
            .HasForeignKey(t => t.GlossaryCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class HazardLabelConfiguration : IEntityTypeConfiguration<HazardLabel>
{
    public void Configure(EntityTypeBuilder<HazardLabel> builder)
    {
        builder.ToTable(TableNames.HazardLabels);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.Slug).HasMaxLength(100);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.Property(e => e.HazardClass).HasMaxLength(100);

        builder.HasIndex(e => e.Slug).IsUnique();
    }
}

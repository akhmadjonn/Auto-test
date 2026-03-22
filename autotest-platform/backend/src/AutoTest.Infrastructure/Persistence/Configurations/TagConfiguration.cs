using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable(TableNames.Tags);
        builder.HasKey(t => t.Id);

        builder.OwnsOne(t => t.Name, b => b.ToJson());

        builder.Property(t => t.Slug).HasMaxLength(100);

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}

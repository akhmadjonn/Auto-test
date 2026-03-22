using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable(TableNames.Announcements);
        builder.HasKey(a => a.Id);

        builder.OwnsOne(a => a.Title, b => b.ToJson());
        builder.OwnsOne(a => a.Content, b => b.ToJson());

        builder.Property(a => a.CreatedBy).HasMaxLength(200);

        builder.HasIndex(a => new { a.IsActive, a.StartsAt, a.ExpiresAt });
    }
}

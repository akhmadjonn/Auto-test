using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class VideoLessonConfiguration : IEntityTypeConfiguration<VideoLesson>
{
    public void Configure(EntityTypeBuilder<VideoLesson> builder)
    {
        builder.ToTable(TableNames.VideoLessons);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Title, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.VideoUrl).HasMaxLength(1000);
        builder.Property(e => e.ThumbnailUrl).HasMaxLength(500);

        builder.HasIndex(e => new { e.VideoCategoryId, e.IsActive, e.SortOrder });

        builder.HasMany(e => e.Attachments)
            .WithOne(a => a.Lesson)
            .HasForeignKey(a => a.VideoLessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

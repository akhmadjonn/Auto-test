using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class LessonAttachmentConfiguration : IEntityTypeConfiguration<LessonAttachment>
{
    public void Configure(EntityTypeBuilder<LessonAttachment> builder)
    {
        builder.ToTable(TableNames.LessonAttachments);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FileName).HasMaxLength(500);
        builder.Property(e => e.FileUrl).HasMaxLength(1000);

        builder.HasIndex(e => e.VideoLessonId);
    }
}

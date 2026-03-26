using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable(TableNames.Questions);
        builder.HasKey(q => q.Id);

        builder.OwnsOne(q => q.Text, b => b.ToJson());
        builder.OwnsOne(q => q.Explanation, b => b.ToJson());

        builder.Property(q => q.ImageUrl).HasMaxLength(500);
        builder.Property(q => q.ThumbnailUrl).HasMaxLength(500);

        builder.Property(q => q.Status)
            .HasDefaultValue(QuestionStatus.Active)
            .HasSentinel(QuestionStatus.Draft);
        builder.Property(q => q.TotalAttempts).HasDefaultValue(0);
        builder.Property(q => q.CorrectCount).HasDefaultValue(0);

        // Filtered index for active questions by category+difficulty (exam pool loading)
        builder.HasIndex(q => new { q.CategoryId, q.Difficulty })
            .HasFilter("\"Status\" = 1");
        builder.HasIndex(q => q.TicketNumber);
        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => new { q.TicketNumber, q.Status });

        // FK: RESTRICT — deleting a category must NOT cascade-delete its questions
        builder.HasOne(q => q.Category)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Tags)
            .WithMany(t => t.Questions)
            .UsingEntity(TableNames.QuestionTags);
    }
}

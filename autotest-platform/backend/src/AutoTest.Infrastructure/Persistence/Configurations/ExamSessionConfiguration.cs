using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.ToTable(TableNames.ExamSessions);
        builder.HasKey(s => s.Id);

        // FK: RESTRICT — deleting a user must NOT wipe exam history
        builder.HasOne(s => s.User)
            .WithMany(u => u.ExamSessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ExamTemplate)
            .WithMany()
            .HasForeignKey(s => s.ExamTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.UserId, s.Status });
        builder.HasIndex(s => new { s.UserId, s.Mode });
        builder.HasIndex(s => new { s.UserId, s.Status, s.Mode, s.CompletedAt });
        builder.HasIndex(s => s.ExpiresAt);

        // Composite index for exam history ordering
        builder.HasIndex(s => new { s.UserId, s.CreatedAt })
            .IsDescending(false, true);

        // Partial index for completed exams
        builder.HasIndex(s => s.CompletedAt)
            .HasFilter("\"Status\" = 1");
    }
}

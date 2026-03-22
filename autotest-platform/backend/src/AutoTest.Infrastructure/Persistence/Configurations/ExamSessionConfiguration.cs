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

        builder.HasOne(s => s.User)
            .WithMany(u => u.ExamSessions)
            .HasForeignKey(s => s.UserId);

        builder.HasOne(s => s.ExamTemplate)
            .WithMany()
            .HasForeignKey(s => s.ExamTemplateId);

        builder.HasIndex(s => new { s.UserId, s.Status });
        builder.HasIndex(s => new { s.UserId, s.Mode });
        builder.HasIndex(s => new { s.UserId, s.Status, s.Mode, s.CompletedAt });
        builder.HasIndex(s => s.ExpiresAt);
    }
}

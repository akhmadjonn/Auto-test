using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class SessionQuestionConfiguration : IEntityTypeConfiguration<SessionQuestion>
{
    public void Configure(EntityTypeBuilder<SessionQuestion> builder)
    {
        builder.ToTable(TableNames.SessionQuestions);
        builder.HasKey(sq => sq.Id);

        builder.HasOne(sq => sq.ExamSession)
            .WithMany(s => s.SessionQuestions)
            .HasForeignKey(sq => sq.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sq => sq.Question)
            .WithMany()
            .HasForeignKey(sq => sq.QuestionId);

        builder.HasOne(sq => sq.SelectedAnswer)
            .WithMany()
            .HasForeignKey(sq => sq.SelectedAnswerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(sq => new { sq.ExamSessionId, sq.Order });
    }
}

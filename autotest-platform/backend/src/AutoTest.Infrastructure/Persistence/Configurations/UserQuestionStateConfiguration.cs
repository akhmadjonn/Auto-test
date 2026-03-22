using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class UserQuestionStateConfiguration : IEntityTypeConfiguration<UserQuestionState>
{
    public void Configure(EntityTypeBuilder<UserQuestionState> builder)
    {
        builder.ToTable(TableNames.UserQuestionStates);

        builder.HasKey(uqs => new { uqs.UserId, uqs.QuestionId });

        builder.HasOne(uqs => uqs.User)
            .WithMany(u => u.UserQuestionStates)
            .HasForeignKey(uqs => uqs.UserId);

        builder.HasOne(uqs => uqs.Question)
            .WithMany()
            .HasForeignKey(uqs => uqs.QuestionId);

        builder.HasIndex(uqs => new { uqs.UserId, uqs.NextReviewDate });
        builder.HasIndex(uqs => new { uqs.UserId, uqs.LastAttemptAt });
    }
}

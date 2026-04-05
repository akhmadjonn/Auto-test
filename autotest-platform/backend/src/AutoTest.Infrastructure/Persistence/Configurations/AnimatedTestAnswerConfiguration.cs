using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class AnimatedTestAnswerConfiguration : IEntityTypeConfiguration<AnimatedTestAnswer>
{
    public void Configure(EntityTypeBuilder<AnimatedTestAnswer> builder)
    {
        builder.ToTable(TableNames.AnimatedTestAnswers);
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Session)
            .WithMany(s => s.Answers)
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.QuestionId).HasMaxLength(20);
        builder.Property(a => a.SelectedOptionId).HasMaxLength(20);
        builder.Property(a => a.Category).HasMaxLength(20);

        builder.HasIndex(a => a.SessionId);
    }
}

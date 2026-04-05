using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class AnimatedTestSessionConfiguration : IEntityTypeConfiguration<AnimatedTestSession>
{
    public void Configure(EntityTypeBuilder<AnimatedTestSession> builder)
    {
        builder.ToTable(TableNames.AnimatedTestSessions);
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.QuestionIds)
            .HasColumnType("text[]");

        builder.HasIndex(s => new { s.UserId, s.CreatedAt })
            .IsDescending(false, true);
    }
}

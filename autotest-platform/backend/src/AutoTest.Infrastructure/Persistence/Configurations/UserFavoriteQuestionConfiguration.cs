using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class UserFavoriteQuestionConfiguration : IEntityTypeConfiguration<UserFavoriteQuestion>
{
    public void Configure(EntityTypeBuilder<UserFavoriteQuestion> builder)
    {
        builder.ToTable(TableNames.UserFavoriteQuestions);

        builder.HasKey(f => new { f.UserId, f.QuestionId });

        builder.HasIndex(f => f.UserId);

        builder.HasOne(f => f.User)
            .WithMany(u => u.FavoriteQuestions)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Question)
            .WithMany(q => q.FavoritedBy)
            .HasForeignKey(f => f.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

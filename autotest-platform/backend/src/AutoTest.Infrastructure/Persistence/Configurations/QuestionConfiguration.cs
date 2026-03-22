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

        builder.HasIndex(q => new { q.CategoryId, q.Difficulty })
            .HasFilter("\"IsActive\" = true");
        builder.HasIndex(q => q.TicketNumber);
        builder.HasIndex(q => q.IsActive);
        builder.HasIndex(q => new { q.TicketNumber, q.IsActive });

        builder.HasOne(q => q.Category)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.CategoryId);

        builder.HasMany(q => q.Tags)
            .WithMany(t => t.Questions)
            .UsingEntity(TableNames.QuestionTags);
    }
}

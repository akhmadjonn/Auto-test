using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class ExamPoolRuleConfiguration : IEntityTypeConfiguration<ExamPoolRule>
{
    public void Configure(EntityTypeBuilder<ExamPoolRule> builder)
    {
        builder.ToTable(TableNames.ExamPoolRules);
        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.ExamTemplate)
            .WithMany(t => t.PoolRules)
            .HasForeignKey(r => r.ExamTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

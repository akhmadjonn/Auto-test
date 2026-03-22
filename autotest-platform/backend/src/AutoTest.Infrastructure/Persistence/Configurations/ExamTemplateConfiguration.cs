using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class ExamTemplateConfiguration : IEntityTypeConfiguration<ExamTemplate>
{
    public void Configure(EntityTypeBuilder<ExamTemplate> builder)
    {
        builder.ToTable(TableNames.ExamTemplates);
        builder.HasKey(t => t.Id);

        builder.OwnsOne(t => t.Title, b => b.ToJson());
    }
}

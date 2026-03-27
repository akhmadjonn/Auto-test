using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class FirstAidStepConfiguration : IEntityTypeConfiguration<FirstAidStep>
{
    public void Configure(EntityTypeBuilder<FirstAidStep> builder)
    {
        builder.ToTable(TableNames.FirstAidSteps);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Title, b => b.ToJson());
        builder.OwnsOne(e => e.Description, b => b.ToJson());

        builder.Property(e => e.ImageUrl).HasMaxLength(500);

        builder.HasIndex(e => new { e.FirstAidProcedureId, e.StepOrder });
    }
}

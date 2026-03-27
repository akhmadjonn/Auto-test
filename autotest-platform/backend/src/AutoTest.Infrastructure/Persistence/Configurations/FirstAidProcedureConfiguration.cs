using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class FirstAidProcedureConfiguration : IEntityTypeConfiguration<FirstAidProcedure>
{
    public void Configure(EntityTypeBuilder<FirstAidProcedure> builder)
    {
        builder.ToTable(TableNames.FirstAidProcedures);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Name, b => b.ToJson());
        builder.OwnsOne(e => e.Summary, b => b.ToJson());

        builder.Property(e => e.Slug).HasMaxLength(100);
        builder.Property(e => e.IconUrl).HasMaxLength(500);

        builder.HasIndex(e => e.Slug).IsUnique();

        builder.HasMany(e => e.Steps)
            .WithOne(s => s.Procedure)
            .HasForeignKey(s => s.FirstAidProcedureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

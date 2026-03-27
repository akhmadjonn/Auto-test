using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class GlossaryTermConfiguration : IEntityTypeConfiguration<GlossaryTerm>
{
    public void Configure(EntityTypeBuilder<GlossaryTerm> builder)
    {
        builder.ToTable(TableNames.GlossaryTerms);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Term, b => b.ToJson());
        builder.OwnsOne(e => e.Definition, b => b.ToJson());

        builder.Property(e => e.RelatedQuestionIds).HasColumnType("uuid[]");

        builder.HasIndex(e => e.GlossaryCategoryId);
    }
}

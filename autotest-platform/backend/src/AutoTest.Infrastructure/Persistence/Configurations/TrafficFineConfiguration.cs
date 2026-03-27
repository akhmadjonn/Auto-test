using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class TrafficFineConfiguration : IEntityTypeConfiguration<TrafficFine>
{
    public void Configure(EntityTypeBuilder<TrafficFine> builder)
    {
        builder.ToTable(TableNames.TrafficFines);
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.ViolationDescription, b => b.ToJson());
        builder.OwnsOne(e => e.AdditionalNotes, b => b.ToJson());

        builder.Property(e => e.ArticleNumber).HasMaxLength(50);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);

        builder.HasIndex(e => e.ArticleNumber);
        builder.HasIndex(e => e.IsActive);
    }
}

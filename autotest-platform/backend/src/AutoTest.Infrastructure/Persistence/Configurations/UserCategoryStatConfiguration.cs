using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class UserCategoryStatConfiguration : IEntityTypeConfiguration<UserCategoryStat>
{
    public void Configure(EntityTypeBuilder<UserCategoryStat> builder)
    {
        builder.ToTable(TableNames.UserCategoryStats);

        builder.HasKey(ucs => new { ucs.UserId, ucs.CategoryId });

        builder.HasOne(ucs => ucs.User)
            .WithMany()
            .HasForeignKey(ucs => ucs.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ucs => ucs.Category)
            .WithMany()
            .HasForeignKey(ucs => ucs.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

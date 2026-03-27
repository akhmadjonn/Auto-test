using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class UserDailyStatsConfiguration : IEntityTypeConfiguration<UserDailyStats>
{
    public void Configure(EntityTypeBuilder<UserDailyStats> builder)
    {
        builder.ToTable(TableNames.UserDailyStats);

        builder.HasKey(s => new { s.UserId, s.StatDate });

        builder.HasIndex(s => new { s.UserId, s.StatDate })
            .IsDescending(false, true);

        builder.HasOne(s => s.User)
            .WithMany(u => u.DailyStats)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

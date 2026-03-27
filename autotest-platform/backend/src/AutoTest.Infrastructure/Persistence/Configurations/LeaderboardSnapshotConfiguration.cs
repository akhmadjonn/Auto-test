using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class LeaderboardSnapshotConfiguration : IEntityTypeConfiguration<LeaderboardSnapshot>
{
    public void Configure(EntityTypeBuilder<LeaderboardSnapshot> builder)
    {
        builder.ToTable(TableNames.LeaderboardSnapshots);

        builder.HasKey(l => l.Id);

        builder.HasIndex(l => new { l.Period, l.SnapshotDate, l.Rank });
        builder.HasIndex(l => new { l.UserId, l.Period });

        builder.Property(l => l.Period).HasMaxLength(20);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

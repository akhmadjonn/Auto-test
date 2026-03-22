using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable(TableNames.UserDevices);
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceId).HasMaxLength(500);
        builder.Property(d => d.DeviceName).HasMaxLength(200);
        builder.Property(d => d.FcmToken).HasMaxLength(500);

        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();
    }
}

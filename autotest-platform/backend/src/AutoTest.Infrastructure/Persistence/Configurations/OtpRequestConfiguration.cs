using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class OtpRequestConfiguration : IEntityTypeConfiguration<OtpRequest>
{
    public void Configure(EntityTypeBuilder<OtpRequest> builder)
    {
        builder.ToTable(TableNames.OtpRequests);
        builder.HasKey(o => o.Id);

        builder.Property(o => o.PhoneNumber).HasMaxLength(20);
        builder.Property(o => o.CodeHash).HasMaxLength(200);

        builder.HasIndex(o => new { o.PhoneNumber, o.IsVerified });
    }
}

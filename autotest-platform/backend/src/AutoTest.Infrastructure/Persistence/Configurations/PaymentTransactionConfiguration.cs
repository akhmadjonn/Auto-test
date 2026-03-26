using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable(TableNames.PaymentTransactions);
        builder.HasKey(pt => pt.Id);

        builder.Property(pt => pt.ProviderTransactionId).HasMaxLength(200);
        builder.Property(pt => pt.Currency).HasMaxLength(10).HasDefaultValue("UZS");

        // FK: RESTRICT — deleting a user must NOT wipe payment history
        builder.HasOne(pt => pt.User)
            .WithMany()
            .HasForeignKey(pt => pt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pt => pt.Subscription)
            .WithMany()
            .HasForeignKey(pt => pt.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pt => new { pt.UserId, pt.Status });
        builder.HasIndex(pt => new { pt.CreatedAt, pt.Status });
        builder.HasIndex(pt => new { pt.Status, pt.Provider });
    }
}

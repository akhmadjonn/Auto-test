using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoTest.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable(TableNames.SubscriptionPlans);
        builder.HasKey(p => p.Id);

        builder.OwnsOne(p => p.Name, b => b.ToJson());
        builder.OwnsOne(p => p.Description, b => b.ToJson());

        builder.Property(p => p.Features).HasColumnType("jsonb");
    }
}

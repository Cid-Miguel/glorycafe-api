using GloryCafe.Domain.Entities;
using GloryCafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GloryCafe.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerFirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.CustomerLastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.CustomerPhone)
            .HasMaxLength(30);

        builder.Property(o => o.CustomerEmail)
            .HasMaxLength(200);

        builder.Property(o => o.EstimatedPickupTime)
            .IsRequired();

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(OrderStatus.Pending);

        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        builder.Property(o => o.OrderDate)
            .IsRequired();

        builder.Property(o => o.DailyOrderNumber)
            .IsRequired();

        builder.Property(o => o.StripePaymentIntentId)
            .HasMaxLength(100);

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => new { o.OrderDate, o.DailyOrderNumber }).IsUnique();

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

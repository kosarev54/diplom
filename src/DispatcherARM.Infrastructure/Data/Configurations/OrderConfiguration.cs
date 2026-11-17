using DispatcherARM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DispatcherARM.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация таблицы заказов
/// </summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        builder.Property(o => o.ClientName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.ClientPhone)
            .HasMaxLength(20);

        builder.Property(o => o.Address)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.DeliveryDate);
    }
}

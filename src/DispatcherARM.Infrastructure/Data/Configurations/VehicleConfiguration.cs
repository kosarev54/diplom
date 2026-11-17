using DispatcherARM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DispatcherARM.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация таблицы транспортных средств
/// </summary>
public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.LicensePlate)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(v => v.LicensePlate)
            .IsUnique();

        builder.Property(v => v.Model)
            .HasMaxLength(100)
            .IsRequired();
    }
}

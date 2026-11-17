using DispatcherARM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DispatcherARM.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация таблицы водителей
/// </summary>
public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Phone)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.LicenseNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(d => d.LicenseNumber)
            .IsUnique();

        builder.HasOne(d => d.AssignedVehicle)
            .WithMany(v => v.Drivers)
            .HasForeignKey(d => d.AssignedVehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

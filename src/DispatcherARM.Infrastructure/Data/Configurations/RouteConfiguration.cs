using DispatcherARM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DispatcherARM.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация таблицы маршрутов
/// </summary>
public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(200);

        builder.Property(r => r.OptimizationMethod)
            .HasMaxLength(100);

        builder.HasOne(r => r.Driver)
            .WithMany(d => d.Routes)
            .HasForeignKey(r => r.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Vehicle)
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Waypoints)
            .WithOne(w => w.Route)
            .HasForeignKey(w => w.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.Date);
    }
}

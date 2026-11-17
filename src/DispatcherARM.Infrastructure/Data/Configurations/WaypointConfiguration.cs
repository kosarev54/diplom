using DispatcherARM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DispatcherARM.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация таблицы точек маршрута
/// </summary>
public class WaypointConfiguration : IEntityTypeConfiguration<Waypoint>
{
    public void Configure(EntityTypeBuilder<Waypoint> builder)
    {
        builder.ToTable("waypoints");

        builder.HasKey(w => w.Id);

        builder.HasOne(w => w.Route)
            .WithMany(r => r.Waypoints)
            .HasForeignKey(w => w.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Order)
            .WithOne(o => o.Waypoint)
            .HasForeignKey<Waypoint>(w => w.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.RouteId, w.SequenceNumber })
            .IsUnique();
    }
}

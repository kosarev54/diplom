using DispatcherARM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Data;

/// <summary>
/// Контекст базы данных приложения
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>Пользователи</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Водители</summary>
    public DbSet<Driver> Drivers => Set<Driver>();

    /// <summary>Транспортные средства</summary>
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    /// <summary>Заказы</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Маршруты</summary>
    public DbSet<Route> Routes => Set<Route>();

    /// <summary>Точки маршрутов</summary>
    public DbSet<Waypoint> Waypoints => Set<Waypoint>();

    /// <summary>Финансовые транзакции водителей</summary>
    public DbSet<DriverTransaction> DriverTransactions => Set<DriverTransaction>();

    /// <summary>Позиции заказов (товары)</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Order → AssignedDriver
        modelBuilder.Entity<Order>()
            .HasOne(o => o.AssignedDriver)
            .WithMany()
            .HasForeignKey(o => o.AssignedDriverId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order → OrderItems (каскадное удаление)
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

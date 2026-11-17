using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Маршрут доставки
/// </summary>
public class Route : BaseEntity
{
    /// <summary>Название маршрута</summary>
    public string? Name { get; set; }

    /// <summary>ID водителя</summary>
    public int DriverId { get; set; }

    /// <summary>ID транспортного средства</summary>
    public int VehicleId { get; set; }

    /// <summary>Дата маршрута</summary>
    public DateTime Date { get; set; }

    /// <summary>Статус маршрута</summary>
    public RouteStatus Status { get; set; } = RouteStatus.Draft;

    /// <summary>Общая дистанция (км)</summary>
    public double TotalDistanceKm { get; set; }

    /// <summary>Общее время в пути (мин)</summary>
    public double TotalTimeMinutes { get; set; }

    /// <summary>Метод оптимизации</summary>
    public string? OptimizationMethod { get; set; }

    /// <summary>Оценка расхода топлива (л)</summary>
    public double? FuelCostEstimate { get; set; }

    // Навигационные свойства

    /// <summary>Водитель</summary>
    public virtual Driver Driver { get; set; } = null!;

    /// <summary>Транспортное средство</summary>
    public virtual Vehicle Vehicle { get; set; } = null!;

    /// <summary>Точки маршрута</summary>
    public virtual ICollection<Waypoint> Waypoints { get; set; } = new List<Waypoint>();
}

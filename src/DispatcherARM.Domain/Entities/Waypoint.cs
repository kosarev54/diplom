using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Точка маршрута (waypoint)
/// </summary>
public class Waypoint : BaseEntity
{
    /// <summary>ID маршрута</summary>
    public int RouteId { get; set; }

    /// <summary>ID заказа</summary>
    public int OrderId { get; set; }

    /// <summary>Порядковый номер в маршруте</summary>
    public int SequenceNumber { get; set; }

    /// <summary>Широта</summary>
    public double Latitude { get; set; }

    /// <summary>Долгота</summary>
    public double Longitude { get; set; }

    /// <summary>Расчётное время прибытия</summary>
    public DateTime? EstimatedArrival { get; set; }

    /// <summary>Фактическое время прибытия</summary>
    public DateTime? ActualArrival { get; set; }

    /// <summary>Расчётное время отправления</summary>
    public DateTime? EstimatedDeparture { get; set; }

    /// <summary>Фактическое время отправления</summary>
    public DateTime? ActualDeparture { get; set; }

    /// <summary>Статус точки</summary>
    public OrderStatus Status { get; set; } = OrderStatus.New;

    // Навигационные свойства

    /// <summary>Маршрут</summary>
    public virtual Route Route { get; set; } = null!;

    /// <summary>Заказ</summary>
    public virtual Order Order { get; set; } = null!;
}

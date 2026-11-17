namespace DispatcherARM.Domain.Models;

/// <summary>
/// Результат оптимизации маршрута
/// </summary>
public class RouteOptimizationResult
{
    /// <summary>Упорядоченные точки маршрута</summary>
    public List<OptimizedWaypoint> OrderedWaypoints { get; set; } = new();

    /// <summary>Общая дистанция (км)</summary>
    public double TotalDistanceKm { get; set; }

    /// <summary>Общее время в пути (мин)</summary>
    public double TotalTimeMinutes { get; set; }

    /// <summary>Геометрия маршрута (encoded polyline)</summary>
    public string? RouteGeometry { get; set; }

    /// <summary>Успешность оптимизации</summary>
    public bool Success { get; set; }

    /// <summary>Сообщение об ошибке</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Название использованного алгоритма</summary>
    public string? AlgorithmName { get; set; }

    /// <summary>Время выполнения алгоритма (мс)</summary>
    public long ElapsedMilliseconds { get; set; }
}

/// <summary>
/// Оптимизированная точка маршрута
/// </summary>
public class OptimizedWaypoint
{
    /// <summary>ID заказа</summary>
    public int OrderId { get; set; }

    /// <summary>Широта</summary>
    public double Latitude { get; set; }

    /// <summary>Долгота</summary>
    public double Longitude { get; set; }

    /// <summary>Порядковый номер</summary>
    public int SequenceNumber { get; set; }

    /// <summary>Расчётное время прибытия</summary>
    public DateTime? EstimatedArrival { get; set; }
}

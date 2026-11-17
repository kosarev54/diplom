using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Models;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Интерфейс оптимизатора маршрутов
/// </summary>
public interface IRouteOptimizer
{
    /// <summary>Название алгоритма</summary>
    string AlgorithmName { get; }

    /// <summary>
    /// Построить оптимальный маршрут для набора заказов
    /// </summary>
    /// <param name="orders">Заказы для включения в маршрут</param>
    /// <param name="vehicle">Транспортное средство</param>
    /// <param name="depotLocation">Координаты начальной точки (склад/база)</param>
    /// <returns>Результат оптимизации</returns>
    Task<RouteOptimizationResult> OptimizeAsync(
        IEnumerable<Order> orders,
        Vehicle vehicle,
        (double Lat, double Lon) depotLocation);
}

using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Domain.Models;
using DispatcherARM.Infrastructure.Routing;

namespace DispatcherARM.Application.Services;

/// <summary>
/// Оптимизатор маршрутов через OSRM Trip API (встроенный TSP-солвер)
/// </summary>
public class OsrmTripOptimizer : IRouteOptimizer
{
    private readonly OsrmClient _osrmClient;

    public string AlgorithmName => "OSRM Trip (TSP)";

    public OsrmTripOptimizer(OsrmClient osrmClient)
    {
        _osrmClient = osrmClient;
    }

    public async Task<RouteOptimizationResult> OptimizeAsync(
        IEnumerable<Order> orders,
        Vehicle vehicle,
        (double Lat, double Lon) depotLocation)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new RouteOptimizationResult { AlgorithmName = AlgorithmName };

        var orderList = orders.ToList();
        if (orderList.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Нет заказов для оптимизации";
            return result;
        }

        try
        {
            // Все координаты (депо + заказы)
            var allCoords = new List<(double Lat, double Lon)> { depotLocation };
            allCoords.AddRange(orderList.Select(o => (o.Latitude, o.Longitude)));

            // Используем OSRM Trip API
            var tripResponse = await _osrmClient.GetTripAsync(allCoords, roundtrip: false);
            if (tripResponse?.Code != "Ok" || tripResponse.Trips.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Не удалось получить оптимальный маршрут от OSRM Trip API";
                return result;
            }

            var trip = tripResponse.Trips[0];

            // Восстанавливаем порядок по waypoint_index
            var waypointOrder = tripResponse.Waypoints
                .Select((wp, originalIndex) => new { wp.WaypointIndex, OriginalIndex = originalIndex })
                .Where(x => x.OriginalIndex > 0) // Пропускаем депо
                .OrderBy(x => x.WaypointIndex)
                .ToList();

            int seq = 1;
            foreach (var wp in waypointOrder)
            {
                var order = orderList[wp.OriginalIndex - 1]; // -1 т.к. депо на индексе 0
                result.OrderedWaypoints.Add(new OptimizedWaypoint
                {
                    OrderId = order.Id,
                    Latitude = order.Latitude,
                    Longitude = order.Longitude,
                    SequenceNumber = seq++
                });
            }

            result.TotalDistanceKm = trip.Distance / 1000.0;
            result.TotalTimeMinutes = trip.Duration / 60.0;
            result.RouteGeometry = trip.Geometry;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Ошибка оптимизации: {ex.Message}";
        }

        sw.Stop();
        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        return result;
    }
}

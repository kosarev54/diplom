using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Domain.Models;
using DispatcherARM.Infrastructure.Routing;

namespace DispatcherARM.Application.Services;

/// <summary>
/// Оптимизатор маршрутов с улучшением 2-opt
/// Сначала строит маршрут методом ближайшего соседа, затем улучшает перестановками
/// </summary>
public class TwoOptOptimizer : IRouteOptimizer
{
    private readonly OsrmClient _osrmClient;

    public string AlgorithmName => "Nearest Neighbor + 2-opt";

    public TwoOptOptimizer(OsrmClient osrmClient)
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

            // Матрица расстояний
            var tableResponse = await _osrmClient.GetTableAsync(allCoords);
            if (tableResponse?.Code != "Ok" || tableResponse.Durations == null)
            {
                result.Success = false;
                result.ErrorMessage = "Не удалось получить матрицу расстояний от OSRM";
                return result;
            }

            var durations = tableResponse.Durations;
            var n = allCoords.Count;

            // Шаг 1: Nearest Neighbor для начального решения
            var route = BuildNearestNeighborRoute(durations, n);

            // Шаг 2: Улучшение 2-opt
            route = Improve2Opt(route, durations);

            // Формируем результат
            int seq = 1;
            foreach (var idx in route.Where(i => i > 0)) // Пропускаем депо
            {
                var order = orderList[idx - 1];
                result.OrderedWaypoints.Add(new OptimizedWaypoint
                {
                    OrderId = order.Id,
                    Latitude = order.Latitude,
                    Longitude = order.Longitude,
                    SequenceNumber = seq++
                });
            }

            // Получаем геометрию маршрута
            var routeCoords = new List<(double Lat, double Lon)> { depotLocation };
            routeCoords.AddRange(result.OrderedWaypoints.Select(w => (w.Latitude, w.Longitude)));

            var routeResponse = await _osrmClient.GetRouteAsync(routeCoords);
            if (routeResponse?.Code == "Ok" && routeResponse.Routes.Count > 0)
            {
                var osrmRoute = routeResponse.Routes[0];
                result.TotalDistanceKm = osrmRoute.Distance / 1000.0;
                result.TotalTimeMinutes = osrmRoute.Duration / 60.0;
                result.RouteGeometry = osrmRoute.Geometry;
            }

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

    /// <summary>
    /// Построение начального маршрута методом ближайшего соседа
    /// </summary>
    private static List<int> BuildNearestNeighborRoute(double?[][] durations, int n)
    {
        var visited = new bool[n];
        var route = new List<int> { 0 };
        visited[0] = true;

        for (int step = 1; step < n; step++)
        {
            int current = route[^1];
            int nearest = -1;
            double minDuration = double.MaxValue;

            for (int j = 1; j < n; j++)
            {
                if (!visited[j] && durations[current]![j].HasValue && durations[current]![j]!.Value < minDuration)
                {
                    minDuration = durations[current]![j]!.Value;
                    nearest = j;
                }
            }

            if (nearest == -1) break;
            visited[nearest] = true;
            route.Add(nearest);
        }

        return route;
    }

    /// <summary>
    /// Улучшение маршрута алгоритмом 2-opt
    /// Перебирает все пары рёбер и инвертирует сегмент, если это уменьшает длину
    /// </summary>
    private static List<int> Improve2Opt(List<int> route, double?[][] durations)
    {
        bool improved = true;
        int maxIterations = 1000;
        int iteration = 0;

        while (improved && iteration < maxIterations)
        {
            improved = false;
            iteration++;

            for (int i = 1; i < route.Count - 1; i++)
            {
                for (int j = i + 1; j < route.Count; j++)
                {
                    // ── P0-7 FIX: маршрут открытый — нет ребра возврата в депо ──────────
                    // Если j — последняя точка, у неё нет исходящего ребра, стоимость = 0
                    double outJ    = j + 1 < route.Count ? GetEdgeCost(durations, route[j], route[j + 1]) : 0;
                    double outI    = j + 1 < route.Count ? GetEdgeCost(durations, route[i], route[j + 1]) : 0;

                    double currentDist = GetEdgeCost(durations, route[i - 1], route[i]) + outJ;
                    double newDist     = GetEdgeCost(durations, route[i - 1], route[j]) + outI;

                    if (newDist < currentDist)
                    {
                        // Инвертируем сегмент [i, j]
                        route.Reverse(i, j - i + 1);
                        improved = true;
                    }
                }
            }
        }

        return route;
    }

    /// <summary>
    /// Получить стоимость ребра между двумя точками
    /// </summary>
    private static double GetEdgeCost(double?[][] durations, int from, int to)
    {
        return durations[from]?[to] ?? double.MaxValue;
    }
}

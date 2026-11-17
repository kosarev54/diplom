using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Domain.Models;
using DispatcherARM.Infrastructure.Routing;

namespace DispatcherARM.Application.Services;

/// <summary>
/// Сервис оптимизации маршрутов — алгоритм ближайшего соседа (Nearest Neighbor)
/// </summary>
public class NearestNeighborOptimizer : IRouteOptimizer
{
    private readonly OsrmClient _osrmClient;

    public string AlgorithmName => "Nearest Neighbor";

    public NearestNeighborOptimizer(OsrmClient osrmClient)
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
            // Собираем все координаты (депо + заказы)
            var allCoords = new List<(double Lat, double Lon)> { depotLocation };
            allCoords.AddRange(orderList.Select(o => (o.Latitude, o.Longitude)));

            // Получаем матрицу расстояний через OSRM
            var tableResponse = await _osrmClient.GetTableAsync(allCoords);
            if (tableResponse == null || tableResponse.Code != "Ok" || tableResponse.Durations == null)
            {
                result.Success = false;
                result.ErrorMessage = "Не удалось получить матрицу расстояний от OSRM";
                return result;
            }

            var durations = tableResponse.Durations;
            var n = allCoords.Count;

            // Алгоритм ближайшего соседа
            var visited = new bool[n];
            var route = new List<int> { 0 }; // Начинаем с депо
            visited[0] = true;

            for (int step = 1; step < n; step++)
            {
                int current = route[^1];
                int nearest = -1;
                double minDuration = double.MaxValue;

                for (int j = 1; j < n; j++) // Пропускаем депо (0)
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

            // Формируем результат
            int seq = 1;
            foreach (var idx in route.Skip(1)) // Пропускаем депо
            {
                var order = orderList[idx - 1]; // -1 т.к. депо занимает индекс 0
                result.OrderedWaypoints.Add(new OptimizedWaypoint
                {
                    OrderId = order.Id,
                    Latitude = order.Latitude,
                    Longitude = order.Longitude,
                    SequenceNumber = seq++
                });
            }

            // Получаем реальный маршрут через OSRM Route API
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
}

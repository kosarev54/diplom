using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Domain.Models;

namespace DispatcherARM.Application.Services;

/// <summary>
/// Сервис управления маршрутами и оптимизации
/// </summary>
public class RouteService
{
    private readonly IRouteRepository _routeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IEnumerable<IRouteOptimizer> _optimizers;

    /// <summary>Координаты депо — устанавливаются из AppSettings при регистрации сервиса</summary>
    public (double Lat, double Lon) DepotLocation { get; set; } = (55.7558, 37.6173);

    public RouteService(
        IRouteRepository routeRepository,
        IOrderRepository orderRepository,
        IDriverRepository driverRepository,
        IVehicleRepository vehicleRepository,
        IEnumerable<IRouteOptimizer> optimizers)
    {
        _routeRepository = routeRepository;
        _orderRepository = orderRepository;
        _driverRepository = driverRepository;
        _vehicleRepository = vehicleRepository;
        _optimizers = optimizers;
    }

    /// <summary>
    /// Получить список доступных алгоритмов оптимизации
    /// </summary>
    public IEnumerable<string> GetAvailableAlgorithms()
    {
        return _optimizers.Select(o => o.AlgorithmName);
    }

    /// <summary>
    /// Построить оптимальный маршрут для выбранных заказов
    /// </summary>
    /// <param name="orderIds">ID заказов</param>
    /// <param name="driverId">ID водителя</param>
    /// <param name="vehicleId">ID ТС</param>
    /// <param name="algorithmName">Название алгоритма</param>
    /// <returns>Созданный маршрут</returns>
    public async Task<(Route? Route, RouteOptimizationResult Result)> BuildRouteAsync(
        IEnumerable<int> orderIds,
        int driverId,
        int vehicleId,
        string algorithmName)
    {
        // Получаем данные
        var orders = new List<Order>();
        foreach (var id in orderIds)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order != null) orders.Add(order);
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
        if (vehicle == null)
            return (null, new RouteOptimizationResult { Success = false, ErrorMessage = "ТС не найдено" });

        // Выбираем алгоритм
        var optimizer = _optimizers.FirstOrDefault(o => o.AlgorithmName == algorithmName)
                       ?? _optimizers.First();

        // Оптимизируем
        var result = await optimizer.OptimizeAsync(orders, vehicle, DepotLocation);

        if (!result.Success)
            return (null, result);

        // Создаём маршрут в БД
        var route = new Route
        {
            Name = $"Маршрут {DateTime.Now:dd.MM.yyyy HH:mm}",
            DriverId = driverId,
            VehicleId = vehicleId,
            Date = DateTime.Today,
            Status = RouteStatus.Planned,
            TotalDistanceKm = result.TotalDistanceKm,
            TotalTimeMinutes = result.TotalTimeMinutes,
            OptimizationMethod = result.AlgorithmName,
            FuelCostEstimate = result.TotalDistanceKm * vehicle.FuelConsumptionPer100Km / 100.0
        };

        await _routeRepository.AddAsync(route);
        await _routeRepository.SaveChangesAsync();

        // Создаём точки маршрута
        foreach (var wp in result.OrderedWaypoints)
        {
            var waypoint = new Waypoint
            {
                RouteId = route.Id,
                OrderId = wp.OrderId,
                SequenceNumber = wp.SequenceNumber,
                Latitude = wp.Latitude,
                Longitude = wp.Longitude,
                EstimatedArrival = wp.EstimatedArrival,
                Status = OrderStatus.Assigned
            };
            route.Waypoints.Add(waypoint);

            // Обновляем статус заказа
            var order = orders.First(o => o.Id == wp.OrderId);
            order.Status = OrderStatus.Assigned;
            _orderRepository.Update(order);
        }

        await _routeRepository.SaveChangesAsync();

        // ── БАГ 2 FIX: проставить водителя и статусы ──────────────────────────
        // Назначаем водителя всем заказам маршрута
        foreach (var order in orders)
        {
            order.AssignedDriverId = driverId;
            _orderRepository.Update(order);
        }

        // Переводим водителя и ТС в статус «В рейсе»
        var driver = await _driverRepository.GetByIdAsync(driverId);
        if (driver != null)
        {
            driver.Status = DriverStatus.OnRoute;
            _driverRepository.Update(driver);
        }
        vehicle.Status = VehicleStatus.OnRoute;
        _vehicleRepository.Update(vehicle);

        await _routeRepository.SaveChangesAsync();

        return (route, result);
    }

    /// <summary>
    /// Сравнить все алгоритмы на одном наборе заказов
    /// </summary>
    public async Task<List<RouteOptimizationResult>> CompareAlgorithmsAsync(
        IEnumerable<int> orderIds, int vehicleId)
    {
        var orders = new List<Order>();
        foreach (var id in orderIds)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order != null) orders.Add(order);
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
        if (vehicle == null) return new List<RouteOptimizationResult>();

        var results = new List<RouteOptimizationResult>();
        foreach (var optimizer in _optimizers)
        {
            var result = await optimizer.OptimizeAsync(orders, vehicle, DepotLocation);
            results.Add(result);
        }

        return results;
    }
}

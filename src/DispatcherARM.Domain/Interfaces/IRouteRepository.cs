using DispatcherARM.Domain.Entities;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Репозиторий маршрутов
/// </summary>
public interface IRouteRepository : IRepository<Route>
{
    /// <summary>Получить маршрут с точками (включая заказы), водителем и ТС</summary>
    Task<Route?> GetRouteWithWaypointsAsync(int id);

    /// <summary>Получить маршруты по дате</summary>
    Task<IEnumerable<Route>> GetRoutesByDateAsync(DateTime date);

    /// <summary>Получить маршруты водителя</summary>
    Task<IEnumerable<Route>> GetRoutesByDriverAsync(int driverId);
}

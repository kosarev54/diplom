using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Репозиторий заказов
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    /// <summary>Получить заказы по статусу</summary>
    Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);

    /// <summary>Получить нераспределённые заказы (статус New)</summary>
    Task<IEnumerable<Order>> GetUnassignedOrdersAsync();

    /// <summary>Получить заказы по дате доставки</summary>
    Task<IEnumerable<Order>> GetOrdersByDateAsync(DateTime date);

    /// <summary>Получить количество заказов по статусам</summary>
    Task<Dictionary<OrderStatus, int>> GetOrderCountsByStatusAsync();
}

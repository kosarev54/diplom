using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Repositories;

/// <summary>
/// Репозиторий заказов
/// </summary>
public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        return await _dbSet.Where(o => o.Status == status).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Order>> GetUnassignedOrdersAsync()
    {
        return await _dbSet.Where(o => o.Status == OrderStatus.New).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Order>> GetOrdersByDateAsync(DateTime date)
    {
        return await _dbSet
            .Where(o => o.DeliveryDate.HasValue && o.DeliveryDate.Value.Date == date.Date)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<OrderStatus, int>> GetOrderCountsByStatusAsync()
    {
        return await _dbSet
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}

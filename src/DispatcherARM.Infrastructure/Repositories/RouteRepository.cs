using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Repositories;

/// <summary>
/// Репозиторий маршрутов
/// </summary>
public class RouteRepository : Repository<Route>, IRouteRepository
{
    public RouteRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<Route?> GetRouteWithWaypointsAsync(int id)
    {
        return await _dbSet
            .Include(r => r.Waypoints.OrderBy(w => w.SequenceNumber))
                .ThenInclude(w => w.Order)
            .Include(r => r.Driver)
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Route>> GetRoutesByDateAsync(DateTime date)
    {
        return await _dbSet
            .Where(r => r.Date.Date == date.Date)
            .Include(r => r.Driver)
            .Include(r => r.Vehicle)
            .Include(r => r.Waypoints)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Route>> GetRoutesByDriverAsync(int driverId)
    {
        return await _dbSet
            .Where(r => r.DriverId == driverId)
            .Include(r => r.Vehicle)
            .Include(r => r.Waypoints)
            .OrderByDescending(r => r.Date)
            .ToListAsync();
    }
}

using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Repositories;

/// <summary>
/// Репозиторий водителей
/// </summary>
public class DriverRepository : Repository<Driver>, IDriverRepository
{
    public DriverRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Driver>> GetAvailableDriversAsync()
    {
        return await _dbSet
            .Where(d => d.Status == DriverStatus.Available)
            .Include(d => d.AssignedVehicle)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Driver?> GetDriverWithRoutesAsync(int id)
    {
        return await _dbSet
            .Include(d => d.Routes)
                .ThenInclude(r => r.Waypoints)
            .Include(d => d.AssignedVehicle)
            .FirstOrDefaultAsync(d => d.Id == id);
    }
}

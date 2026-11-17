using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Repositories;

/// <summary>
/// Репозиторий транспортных средств
/// </summary>
public class VehicleRepository : Repository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync()
    {
        return await _dbSet
            .Where(v => v.Status == VehicleStatus.Available)
            .ToListAsync();
    }
}

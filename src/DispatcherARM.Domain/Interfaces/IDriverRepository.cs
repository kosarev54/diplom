using DispatcherARM.Domain.Entities;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Репозиторий водителей
/// </summary>
public interface IDriverRepository : IRepository<Driver>
{
    /// <summary>Получить доступных водителей</summary>
    Task<IEnumerable<Driver>> GetAvailableDriversAsync();

    /// <summary>Получить водителя с маршрутами</summary>
    Task<Driver?> GetDriverWithRoutesAsync(int id);
}

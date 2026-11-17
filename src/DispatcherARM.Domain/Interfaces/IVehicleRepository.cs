using DispatcherARM.Domain.Entities;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Репозиторий транспортных средств
/// </summary>
public interface IVehicleRepository : IRepository<Vehicle>
{
    /// <summary>Получить доступные ТС</summary>
    Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync();
}

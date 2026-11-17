using DispatcherARM.Domain.Entities;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Репозиторий финансовых транзакций водителей
/// </summary>
public interface ITransactionRepository : IRepository<DriverTransaction>
{
    /// <summary>Получить транзакции по ID водителя</summary>
    Task<IEnumerable<DriverTransaction>> GetByDriverIdAsync(int driverId);

    /// <summary>Получить транзакции по ID пользователя (через Driver.UserId)</summary>
    Task<IEnumerable<DriverTransaction>> GetByDriverUserIdAsync(int userId);

    /// <summary>Суммарный баланс водителя (начисления + бонусы − выводы − штрафы)</summary>
    Task<decimal> GetBalanceAsync(int driverId);

    /// <summary>Неподтверждённые транзакции водителя</summary>
    Task<IEnumerable<DriverTransaction>> GetUnconfirmedAsync(int driverId);

    /// <summary>Сумма ожидающих начислений водителя</summary>
    Task<decimal> GetPendingAsync(int driverId);
}

using DispatcherARM.Domain.Entities;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Репозиторий пользователей
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>Получить пользователя по логину</summary>
    Task<User?> GetByLoginAsync(string login);

    /// <summary>Проверить существование пользователя</summary>
    Task<bool> ExistsAsync(string login);
}

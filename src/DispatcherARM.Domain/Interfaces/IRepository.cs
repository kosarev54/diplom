using System.Linq.Expressions;
using DispatcherARM.Domain.Entities;

namespace DispatcherARM.Domain.Interfaces;

/// <summary>
/// Базовый интерфейс репозитория
/// </summary>
/// <typeparam name="T">Тип сущности</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Получить сущность по идентификатору</summary>
    Task<T?> GetByIdAsync(int id);

    /// <summary>Получить все сущности</summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>Найти сущности по условию</summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>Добавить новую сущность</summary>
    Task AddAsync(T entity);

    /// <summary>Обновить сущность</summary>
    void Update(T entity);

    /// <summary>Удалить сущность</summary>
    void Delete(T entity);

    /// <summary>Сохранить изменения</summary>
    Task<int> SaveChangesAsync();
}

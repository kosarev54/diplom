using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Repositories;

/// <summary>
/// Репозиторий пользователей
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<User?> GetByLoginAsync(string login)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Login == login);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string login)
    {
        return await _dbSet.AnyAsync(u => u.Login == login);
    }
}

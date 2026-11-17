using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Repositories;

public class TransactionRepository : Repository<DriverTransaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<DriverTransaction>> GetByDriverIdAsync(int driverId)
        => await _dbSet.Where(t => t.DriverId == driverId)
                       .Include(t => t.Order)
                       .Include(t => t.Route)
                       .OrderByDescending(t => t.CreatedAt)
                       .ToListAsync();

    public async Task<IEnumerable<DriverTransaction>> GetByDriverUserIdAsync(int userId)
    {
        // Найдём driverId по userId
        var driver = await _context.Set<Driver>()
            .FirstOrDefaultAsync(d => d.UserId == userId);
        if (driver == null) return Enumerable.Empty<DriverTransaction>();
        return await GetByDriverIdAsync(driver.Id);
    }

    public async Task<decimal> GetBalanceAsync(int driverId)
    {
        var earnings = await _dbSet
            .Where(t => t.DriverId == driverId && t.IsConfirmed
                     && (t.Type == TransactionType.Earning || t.Type == TransactionType.Bonus))
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
        var withdrawals = await _dbSet
            .Where(t => t.DriverId == driverId && t.IsConfirmed
                     && (t.Type == TransactionType.Withdrawal || t.Type == TransactionType.Penalty))
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
        return earnings - withdrawals;
    }

    public async Task<IEnumerable<DriverTransaction>> GetUnconfirmedAsync(int driverId)
        => await _dbSet.Where(t => t.DriverId == driverId && !t.IsConfirmed)
                       .OrderByDescending(t => t.CreatedAt)
                       .ToListAsync();

    public async Task<decimal> GetPendingAsync(int driverId)
        => await _dbSet
            .Where(t => t.DriverId == driverId && !t.IsConfirmed
                     && (t.Type == TransactionType.Earning || t.Type == TransactionType.Bonus))
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
}

using GloryCafe.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Infrastructure.Persistence;

public class PostgresOrderNumberAllocator : IOrderNumberAllocator
{
    private readonly IApplicationDbContext _db;

    public PostgresOrderNumberAllocator(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> AllocateAsync(DateOnly orderDate, CancellationToken cancellationToken)
    {
        // Serialize concurrent allocations for the same business day so that
        // two simultaneous orders never read the same MAX(DailyOrderNumber).
        // pg_advisory_xact_lock auto-releases when the surrounding transaction
        // commits or rolls back, so a failed insert returns its slot.
        var lockKey = (long)orderDate.DayNumber;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);

        var current = await _db.Orders
            .Where(o => o.OrderDate == orderDate)
            .MaxAsync(o => (int?)o.DailyOrderNumber, cancellationToken);

        return (current ?? 0) + 1;
    }
}

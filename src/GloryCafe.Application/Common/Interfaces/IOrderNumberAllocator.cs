namespace GloryCafe.Application.Common.Interfaces;

public interface IOrderNumberAllocator
{
    // Reserves the next per-day order number for the given date.
    // Must be called inside an active transaction; the underlying
    // serialization lock is released when that transaction ends.
    Task<int> AllocateAsync(DateOnly orderDate, CancellationToken cancellationToken);
}

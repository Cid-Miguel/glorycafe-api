using GloryCafe.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Orders.Queries.GetAdminOrders;

public class GetAdminOrdersQueryHandler
    : IRequestHandler<GetAdminOrdersQuery, IReadOnlyList<AdminOrderSummaryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetAdminOrdersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminOrderSummaryDto>> Handle(
        GetAdminOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.Orders.AsNoTracking();

        if (request.Status is { } status)
            query = query.Where(o => o.Status == status);

        return await query
            .OrderBy(o => o.CreatedAt)
            .Select(o => new AdminOrderSummaryDto(
                o.Id,
                o.OrderDate,
                o.DailyOrderNumber,
                o.CustomerFirstName,
                o.CustomerLastName,
                o.EstimatedPickupTime,
                o.Status,
                o.TotalAmount,
                o.Items.Count,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

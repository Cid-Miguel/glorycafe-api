using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Admin.Orders.Queries.GetAdminOrderById;

public class GetAdminOrderByIdQueryHandler
    : IRequestHandler<GetAdminOrderByIdQuery, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetAdminOrderByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminOrderDetailDto> Handle(
        GetAdminOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == request.Id)
            .Select(o => new AdminOrderDetailDto(
                o.Id,
                o.OrderDate,
                o.DailyOrderNumber,
                o.CustomerFirstName,
                o.CustomerLastName,
                o.CustomerPhone,
                o.CustomerEmail,
                o.EstimatedPickupTime,
                o.Status,
                o.TotalAmount,
                o.CreatedAt,
                o.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new AdminOrderItemDto(
                        i.Id,
                        i.ProductId,
                        i.ProductNameSnapshot,
                        i.UnitPrice,
                        i.Quantity))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
            throw new NotFoundException(nameof(Order), request.Id);

        return order;
    }
}

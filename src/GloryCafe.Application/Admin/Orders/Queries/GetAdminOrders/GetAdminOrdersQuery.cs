using GloryCafe.Domain.Enums;
using MediatR;

namespace GloryCafe.Application.Admin.Orders.Queries.GetAdminOrders;

public record GetAdminOrdersQuery(OrderStatus? Status) : IRequest<IReadOnlyList<AdminOrderSummaryDto>>;

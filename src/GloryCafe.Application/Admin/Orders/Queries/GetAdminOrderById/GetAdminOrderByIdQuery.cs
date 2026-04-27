using MediatR;

namespace GloryCafe.Application.Admin.Orders.Queries.GetAdminOrderById;

public record GetAdminOrderByIdQuery(int Id) : IRequest<AdminOrderDetailDto>;

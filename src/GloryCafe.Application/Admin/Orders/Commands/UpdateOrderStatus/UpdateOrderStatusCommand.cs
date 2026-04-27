using GloryCafe.Domain.Enums;
using MediatR;

namespace GloryCafe.Application.Admin.Orders.Commands.UpdateOrderStatus;

public record UpdateOrderStatusCommand(int Id, OrderStatus NewStatus) : IRequest;

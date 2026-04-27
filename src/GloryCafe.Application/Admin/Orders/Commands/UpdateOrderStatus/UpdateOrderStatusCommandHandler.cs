using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using GloryCafe.Domain.Enums;
using MediatR;

namespace GloryCafe.Application.Admin.Orders.Commands.UpdateOrderStatus;

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand>
{
    private static readonly Dictionary<OrderStatus, OrderStatus> AllowedTransitions = new()
    {
        [OrderStatus.Pending]   = OrderStatus.Preparing,
        [OrderStatus.Preparing] = OrderStatus.Ready,
        [OrderStatus.Ready]     = OrderStatus.Completed
    };

    private readonly IApplicationDbContext _db;

    public UpdateOrderStatusCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.FindAsync(new object[] { request.Id }, cancellationToken);
        if (order is null)
            throw new NotFoundException(nameof(Order), request.Id);

        if (!AllowedTransitions.TryGetValue(order.Status, out var allowedNext) || allowedNext != request.NewStatus)
        {
            throw new ConflictException(
                $"Cannot transition order {order.Id} from {order.Status} to {request.NewStatus}.");
        }

        order.Status = request.NewStatus;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

using MediatR;

namespace GloryCafe.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand(
    string CustomerFirstName,
    string CustomerLastName,
    string? CustomerPhone,
    string? CustomerEmail,
    DateTime EstimatedPickupTime,
    IReadOnlyList<CreateOrderItemDto> Items) : IRequest<CreateOrderResult>;

public record CreateOrderItemDto(int ProductId, int Quantity);

public record CreateOrderResult(
    int OrderId,
    DateOnly OrderDate,
    int DailyOrderNumber,
    decimal TotalAmount);

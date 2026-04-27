using GloryCafe.Domain.Enums;

namespace GloryCafe.Application.Admin.Orders.Queries.GetAdminOrderById;

public record AdminOrderDetailDto(
    int Id,
    string CustomerFirstName,
    string CustomerLastName,
    string? CustomerPhone,
    string? CustomerEmail,
    DateTime EstimatedPickupTime,
    OrderStatus Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<AdminOrderItemDto> Items);

public record AdminOrderItemDto(
    int Id,
    int ProductId,
    string ProductNameSnapshot,
    decimal UnitPrice,
    int Quantity);

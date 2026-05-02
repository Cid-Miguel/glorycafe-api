using GloryCafe.Domain.Enums;

namespace GloryCafe.Application.Admin.Orders.Queries.GetAdminOrders;

public record AdminOrderSummaryDto(
    int Id,
    DateOnly OrderDate,
    int DailyOrderNumber,
    string CustomerFirstName,
    string CustomerLastName,
    DateTime EstimatedPickupTime,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);

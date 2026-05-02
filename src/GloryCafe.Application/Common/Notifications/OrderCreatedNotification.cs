namespace GloryCafe.Application.Common.Notifications;

public record OrderCreatedNotification(
    int Id,
    DateOnly OrderDate,
    int DailyOrderNumber,
    string CustomerFirstName,
    string CustomerLastName,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);

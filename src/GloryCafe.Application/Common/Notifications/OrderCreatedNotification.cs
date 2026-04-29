namespace GloryCafe.Application.Common.Notifications;

public record OrderCreatedNotification(
    int Id,
    string CustomerFirstName,
    string CustomerLastName,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);

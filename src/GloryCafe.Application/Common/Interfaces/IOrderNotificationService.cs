using GloryCafe.Application.Common.Notifications;

namespace GloryCafe.Application.Common.Interfaces;

public interface IOrderNotificationService
{
    Task NotifyOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken = default);
}

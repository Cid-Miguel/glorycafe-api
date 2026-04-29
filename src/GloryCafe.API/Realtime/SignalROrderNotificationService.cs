using GloryCafe.API.Hubs;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Application.Common.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace GloryCafe.API.Realtime;

public class SignalROrderNotificationService : IOrderNotificationService
{
    private readonly IHubContext<OrdersHub> _hubContext;

    public SignalROrderNotificationService(IHubContext<OrdersHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(OrdersHub.OrderCreatedEvent, notification, cancellationToken);
    }
}

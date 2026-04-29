using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GloryCafe.API.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class OrdersHub : Hub
{
    public const string Path = "/hubs/orders";
    public const string OrderCreatedEvent = "orderCreated";
}

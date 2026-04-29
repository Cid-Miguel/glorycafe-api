using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Application.Common.Notifications;
using GloryCafe.Domain.Entities;
using GloryCafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IOrderNotificationService _notifications;

    public CreateOrderCommandHandler(IApplicationDbContext db, IOrderNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var consolidated = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        var productIds = consolidated.Select(i => i.ProductId).ToList();

        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException(nameof(Product), string.Join(",", missing));

        var unavailable = products.Values.Where(p => !p.IsAvailable).Select(p => p.Name).ToList();
        if (unavailable.Count > 0)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["Items"] = unavailable.Select(name => $"Product '{name}' is not available.").ToArray()
            };
            throw new ValidationException(errors);
        }

        var order = new Order
        {
            CustomerFirstName = request.CustomerFirstName.Trim(),
            CustomerLastName = request.CustomerLastName.Trim(),
            CustomerPhone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : request.CustomerPhone.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail.Trim().ToLowerInvariant(),
            EstimatedPickupTime = request.EstimatedPickupTime.ToUniversalTime(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0m;
        foreach (var line in consolidated)
        {
            var product = products[line.ProductId];
            var unitPrice = product.Price;
            total += unitPrice * line.Quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                UnitPrice = unitPrice,
                Quantity = line.Quantity,
                CreatedAt = DateTime.UtcNow
            });
        }

        order.TotalAmount = total;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        await _notifications.NotifyOrderCreatedAsync(
            new OrderCreatedNotification(
                order.Id,
                order.CustomerFirstName,
                order.CustomerLastName,
                order.TotalAmount,
                order.Items.Count,
                order.CreatedAt),
            cancellationToken);

        return new CreateOrderResult(order.Id, order.TotalAmount);
    }
}

using GloryCafe.Application.Common.Exceptions;
using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Application.Common.Notifications;
using GloryCafe.Domain.Entities;
using GloryCafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GloryCafe.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IOrderNumberAllocator _orderNumbers;
    private readonly IOrderNotificationService _notifications;

    public CreateOrderCommandHandler(
        IApplicationDbContext db,
        IClock clock,
        IOrderNumberAllocator orderNumbers,
        IOrderNotificationService notifications)
    {
        _db = db;
        _clock = clock;
        _orderNumbers = orderNumbers;
        _notifications = notifications;
    }

    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var products = await LoadAndValidateProductsAsync(request, cancellationToken);

        var order = await PersistOrderAsync(request, products, cancellationToken);

        await _notifications.NotifyOrderCreatedAsync(
            new OrderCreatedNotification(
                order.Id,
                order.OrderDate,
                order.DailyOrderNumber,
                order.CustomerFirstName,
                order.CustomerLastName,
                order.TotalAmount,
                order.Items.Count,
                order.CreatedAt),
            cancellationToken);

        return new CreateOrderResult(
            order.Id,
            order.OrderDate,
            order.DailyOrderNumber,
            order.TotalAmount);
    }

    private async Task<IReadOnlyDictionary<int, Product>> LoadAndValidateProductsAsync(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var requestedIds = request.Items
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var products = await _db.Products
            .Where(p => requestedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = requestedIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException(nameof(Product), string.Join(",", missing));
        }

        var unavailable = products.Values
            .Where(p => !p.IsAvailable)
            .Select(p => p.Name)
            .ToList();

        if (unavailable.Count > 0)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["Items"] = unavailable.Select(name => $"Product '{name}' is not available.").ToArray(),
            };
            throw new ValidationException(errors);
        }

        return products;
    }

    private async Task<Order> PersistOrderAsync(
        CreateOrderCommand request,
        IReadOnlyDictionary<int, Product> products,
        CancellationToken cancellationToken)
    {
        // The execution strategy retries on transient connection errors. The
        // advisory lock + insert must run as a single transaction inside the
        // strategy so retries replay both steps consistently.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _db.Database.BeginTransactionAsync(cancellationToken);

            var orderDate = _clock.TodayInBusinessTimeZone;
            var dailyNumber = await _orderNumbers.AllocateAsync(orderDate, cancellationToken);

            var order = BuildOrder(request, products, orderDate, dailyNumber);

            _db.Orders.Add(order);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return order;
        });
    }

    private Order BuildOrder(
        CreateOrderCommand request,
        IReadOnlyDictionary<int, Product> products,
        DateOnly orderDate,
        int dailyNumber)
    {
        var createdAt = _clock.UtcNow;

        var order = new Order
        {
            CustomerFirstName = request.CustomerFirstName.Trim(),
            CustomerLastName = request.CustomerLastName.Trim(),
            CustomerPhone = NormalizeOptional(request.CustomerPhone),
            CustomerEmail = NormalizeOptional(request.CustomerEmail)?.ToLowerInvariant(),
            EstimatedPickupTime = request.EstimatedPickupTime.ToUniversalTime(),
            Status = OrderStatus.Pending,
            OrderDate = orderDate,
            DailyOrderNumber = dailyNumber,
            CreatedAt = createdAt,
        };

        var consolidated = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) });

        decimal total = 0m;
        foreach (var line in consolidated)
        {
            var product = products[line.ProductId];
            total += product.Price * line.Quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
                CreatedAt = createdAt,
            });
        }

        order.TotalAmount = total;
        return order;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

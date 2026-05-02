using GloryCafe.Domain.Common;
using GloryCafe.Domain.Enums;

namespace GloryCafe.Domain.Entities;

public class Order : BaseEntity
{
    public string CustomerFirstName { get; set; } = null!;
    public string CustomerLastName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public DateTime EstimatedPickupTime { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }

    public DateOnly OrderDate { get; set; }
    public int DailyOrderNumber { get; set; }

    public string? StripePaymentIntentId { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Domain;

public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string ShippingAddress { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    [ConcurrencyCheck]
    public int Version { get; set; }
}

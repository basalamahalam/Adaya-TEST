namespace OrderManagement.Api.Domain;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

public static class OrderStatusTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}

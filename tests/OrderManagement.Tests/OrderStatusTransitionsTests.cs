using OrderManagement.Api.Domain;

namespace OrderManagement.Tests;

public class OrderStatusTransitionsTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    public void CanTransition_MatchesSpecTable(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStatusTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void TerminalStates_HaveNoValidOutgoingTransition(OrderStatus terminal)
    {
        foreach (var target in Enum.GetValues<OrderStatus>())
        {
            Assert.False(OrderStatusTransitions.CanTransition(terminal, target));
        }
    }
}

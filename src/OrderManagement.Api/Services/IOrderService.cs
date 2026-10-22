using OrderManagement.Api.Domain;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string idempotencyKey, CancellationToken ct);
    Task<OrderResponse> GetOrderAsync(int id, CancellationToken ct);
    Task<PagedResult<OrderResponse>> ListOrdersAsync(OrderListQuery query, CancellationToken ct);
    Task<OrderResponse> UpdateStatusAsync(int id, OrderStatus newStatus, CancellationToken ct);
    Task<OrderResponse> CancelOrderAsync(int id, CancellationToken ct);
}

using System.ComponentModel.DataAnnotations;
using OrderManagement.Api.Domain;

namespace OrderManagement.Api.Dtos;

public record CreateOrderItemRequest(
    [Required] int ProductId,
    [Range(1, int.MaxValue)] int Quantity
);

public record CreateOrderRequest(
    [Required] string CustomerId,
    [Required, MinLength(1)] List<CreateOrderItemRequest> Items,
    [Required] string ShippingAddress
);

public record UpdateOrderStatusRequest(
    [Required] OrderStatus Status
);

public record OrderItemResponse(int ProductId, int Quantity, decimal UnitPrice);

public record OrderResponse(
    int Id,
    string CustomerId,
    OrderStatus Status,
    string ShippingAddress,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    List<OrderItemResponse> Items
);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);

public record OrderListQuery(
    OrderStatus? Status,
    string? CustomerId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20
);

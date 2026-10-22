using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Domain;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Exceptions;
using OrderManagement.Api.Services;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ValidationApiException("Idempotency-Key header is required.");

        var result = await orderService.CreateOrderAsync(request, idempotencyKey, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> Get(int id, CancellationToken ct) =>
        Ok(await orderService.GetOrderAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] string? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new OrderListQuery(status, customerId, from, to, page, pageSize);
        return Ok(await orderService.ListOrdersAsync(query, ct));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request, CancellationToken ct) =>
        Ok(await orderService.UpdateStatusAsync(id, request.Status, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(int id, CancellationToken ct) =>
        Ok(await orderService.CancelOrderAsync(id, ct));
}

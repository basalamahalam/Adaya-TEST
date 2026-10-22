using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Domain;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Exceptions;

namespace OrderManagement.Api.Services;

public class OrderService(AppDbContext db, ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string idempotencyKey, CancellationToken ct)
    {
        var existing = await FindOrderByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            logger.LogInformation("Idempotency replay: key {IdempotencyKey} already mapped to order {OrderId}", idempotencyKey, existing.Id);
            return ToResponse(existing);
        }

        var quantitiesByProduct = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var products = await db.Products
            .Where(p => quantitiesByProduct.Keys.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var productId in quantitiesByProduct.Keys)
        {
            if (!products.ContainsKey(productId))
                throw new ValidationApiException($"Product {productId} does not exist.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var (productId, quantity) in quantitiesByProduct)
        {
            var rowsAffected = await db.Products
                .Where(p => p.Id == productId && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity), ct);

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync(ct);
                logger.LogWarning("Order rejected: insufficient stock for product {ProductId}, requested {Quantity}", productId, quantity);
                throw new UnprocessableApiException($"Insufficient stock for product {productId}.");
            }
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            CustomerId = request.CustomerId,
            ShippingAddress = request.ShippingAddress,
            Status = OrderStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 1,
            Items = quantitiesByProduct
                .Select(kv => new OrderItem { ProductId = kv.Key, Quantity = kv.Value, UnitPrice = products[kv.Key].Price })
                .ToList()
        };
        db.Orders.Add(order);

        try
        {
            await db.SaveChangesAsync(ct);
            db.IdempotencyKeys.Add(new IdempotencyKey { Key = idempotencyKey, OrderId = order.Id, CreatedAtUtc = now });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            logger.LogInformation("Idempotency key race detected for {IdempotencyKey}, returning winner's order", idempotencyKey);

            var winner = await FindOrderByIdempotencyKeyAsync(idempotencyKey, ct);
            if (winner is not null)
                return ToResponse(winner);
            throw;
        }

        logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, order.CustomerId);
        return ToResponse(order);
    }

    public async Task<OrderResponse> GetOrderAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            throw new NotFoundApiException($"Order {id} not found.");
        return ToResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> ListOrdersAsync(OrderListQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var filtered = db.Orders.Include(o => o.Items).AsQueryable();

        if (query.Status is not null)
            filtered = filtered.Where(o => o.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.CustomerId))
            filtered = filtered.Where(o => o.CustomerId == query.CustomerId);
        if (query.From is not null)
            filtered = filtered.Where(o => o.CreatedAtUtc >= query.From);
        if (query.To is not null)
            filtered = filtered.Where(o => o.CreatedAtUtc <= query.To);

        var totalCount = await filtered.CountAsync(ct);
        var items = await filtered
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderResponse>(items.Select(ToResponse).ToList(), page, pageSize, totalCount);
    }

    public async Task<OrderResponse> UpdateStatusAsync(int id, OrderStatus newStatus, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundApiException($"Order {id} not found.");

        if (!OrderStatusTransitions.CanTransition(order.Status, newStatus))
            throw new UnprocessableApiException($"Cannot transition order from {order.Status} to {newStatus}.");

        order.Status = newStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.Version++;

        await SaveStatusChangeAsync(order, returnStock: newStatus == OrderStatus.Cancelled, ct);

        logger.LogInformation("Order {OrderId} status updated to {Status}", order.Id, newStatus);
        return ToResponse(order);
    }

    public async Task<OrderResponse> CancelOrderAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundApiException($"Order {id} not found.");

        if (!OrderStatusTransitions.CanTransition(order.Status, OrderStatus.Cancelled))
            throw new UnprocessableApiException($"Order in status {order.Status} cannot be cancelled.");

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.Version++;

        await SaveStatusChangeAsync(order, returnStock: true, ct);

        logger.LogInformation("Order {OrderId} cancelled, stock returned", order.Id);
        return ToResponse(order);
    }

    private async Task SaveStatusChangeAsync(Order order, bool returnStock, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (returnStock)
            {
                foreach (var item in order.Items)
                {
                    await db.Products
                        .Where(p => p.Id == item.ProductId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity), ct);
                }
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            logger.LogWarning("Concurrency conflict updating order {OrderId}", order.Id);
            throw new ConflictApiException($"Order {order.Id} was modified by another request. Reload and retry.");
        }
    }

    private Task<Order?> FindOrderByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        db.Orders
            .Include(o => o.Items)
            .Where(o => db.IdempotencyKeys.Any(k => k.Key == idempotencyKey && k.OrderId == o.Id))
            .FirstOrDefaultAsync(ct);

    private static OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.ShippingAddress,
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.Quantity, i.UnitPrice)).ToList()
    );
}

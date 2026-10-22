using System.Net;
using System.Net.Http.Json;
using OrderManagement.Api.Domain;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests;

public class OrderApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateOrder_WithoutIdempotencyKey_Returns400()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "No Key Product", stock: 10);

        var response = await client.PostAsJsonAsync("/orders",
            new CreateOrderRequest("cust-1", [new CreateOrderItemRequest(productId, 1)], "addr"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_Returns422AndLeavesStockUnchanged()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Scarce Product", stock: 3);

        var response = await TestHelpers.CreateOrderAsync(client, "unique-key-1", "cust-1", productId, 10);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var product = await (await client.GetAsync($"/products/{productId}")).Content.ReadFromJsonAsync<ProductResponse>(TestHelpers.JsonOptions);
        Assert.Equal(3, product!.StockQuantity);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns422()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Transition Product", stock: 10);
        var createResponse = await TestHelpers.CreateOrderAsync(client, "unique-key-2", "cust-1", productId, 1);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);

        var response = await TestHelpers.UpdateStatusAsync(client, order!.Id, OrderStatus.Shipped);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ReturnsStockAndMarksCancelled()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Cancel Product", stock: 10);
        var createResponse = await TestHelpers.CreateOrderAsync(client, "unique-key-3", "cust-1", productId, 4);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);

        var cancelResponse = await client.PostAsync($"/orders/{order!.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);
        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);

        var product = await (await client.GetAsync($"/products/{productId}")).Content.ReadFromJsonAsync<ProductResponse>(TestHelpers.JsonOptions);
        Assert.Equal(10, product!.StockQuantity);
    }

    [Fact]
    public async Task CancelOrder_Twice_SecondCallReturns422()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Double Cancel Product", stock: 10);
        var createResponse = await TestHelpers.CreateOrderAsync(client, "unique-key-4", "cust-1", productId, 1);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);

        await client.PostAsync($"/orders/{order!.Id}/cancel", content: null);
        var secondCancel = await client.PostAsync($"/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondCancel.StatusCode);
    }

    [Fact]
    public async Task GetOrder_NotFound_Returns404()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/orders/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListOrders_FiltersByCustomerAndStatus()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "List Product", stock: 50);
        await TestHelpers.CreateOrderAsync(client, "list-key-1", "customer-list-a", productId, 1);
        await TestHelpers.CreateOrderAsync(client, "list-key-2", "customer-list-b", productId, 1);

        var response = await client.GetAsync("/orders?customerId=customer-list-a");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>(TestHelpers.JsonOptions);

        Assert.All(result!.Items, o => Assert.Equal("customer-list-a", o.CustomerId));
        Assert.DoesNotContain(result.Items, o => o.CustomerId == "customer-list-b");
    }
}

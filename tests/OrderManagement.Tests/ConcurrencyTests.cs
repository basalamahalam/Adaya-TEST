using System.Net;
using System.Net.Http.Json;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests;

public class ConcurrencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConcurrencyTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ScenarioA_ConcurrentOrders_StockNeverGoesNegative()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Scenario A Product", stock: 15);

        var task1 = TestHelpers.CreateOrderAsync(client, "scenario-a-key-1", "customer-1", productId, 10);
        var task2 = TestHelpers.CreateOrderAsync(client, "scenario-a-key-2", "customer-2", productId, 10);
        var responses = await Task.WhenAll(task1, task2);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, rejected);

        var product = await (await client.GetAsync($"/products/{productId}")).Content.ReadFromJsonAsync<ProductResponse>(TestHelpers.JsonOptions);
        Assert.Equal(5, product!.StockQuantity);
        Assert.True(product.StockQuantity >= 0);
    }

    [Fact]
    public async Task ScenarioA_ManyConcurrentOrders_TotalDeductedNeverExceedsStock()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Scenario A Product (fleet)", stock: 15);

        var tasks = Enumerable.Range(0, 10)
            .Select(i => TestHelpers.CreateOrderAsync(client, $"scenario-a-fleet-{i}", $"customer-{i}", productId, 2))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, succeeded);

        var product = await (await client.GetAsync($"/products/{productId}")).Content.ReadFromJsonAsync<ProductResponse>(TestHelpers.JsonOptions);
        Assert.Equal(1, product!.StockQuantity);
        Assert.True(product.StockQuantity >= 0);
    }

    [Fact]
    public async Task ScenarioB_ConcurrentStatusUpdates_OnlyOneWins()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Scenario B Product", stock: 100);
        var createResponse = await TestHelpers.CreateOrderAsync(client, "scenario-b-create", "customer-1", productId, 1);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);

        await TestHelpers.UpdateStatusAsync(client, order!.Id, Api.Domain.OrderStatus.Confirmed);

        var shipTask = TestHelpers.UpdateStatusAsync(client, order.Id, Api.Domain.OrderStatus.Shipped);
        var cancelTask = TestHelpers.UpdateStatusAsync(client, order.Id, Api.Domain.OrderStatus.Cancelled);
        var responses = await Task.WhenAll(shipTask, cancelTask);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflicted = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, succeeded);
        Assert.Equal(1, conflicted);

        var finalOrder = await (await client.GetAsync($"/orders/{order.Id}")).Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);
        Assert.True(finalOrder!.Status is Api.Domain.OrderStatus.Shipped or Api.Domain.OrderStatus.Cancelled);
    }

    [Fact]
    public async Task ScenarioC_ConcurrentCreateWithSameIdempotencyKey_OnlyOneOrderCreated()
    {
        using var client = _factory.CreateClient();
        var productId = await TestHelpers.CreateProductAsync(client, "Scenario C Product", stock: 100);
        const string sharedKey = "scenario-c-shared-key";

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => TestHelpers.CreateOrderAsync(client, sharedKey, "customer-1", productId, 1))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var orderIds = new List<int>();
        foreach (var response in responses)
        {
            Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);
            var order = await response.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);
            orderIds.Add(order!.Id);
        }

        Assert.Single(orderIds.Distinct());

        var product = await (await client.GetAsync($"/products/{productId}")).Content.ReadFromJsonAsync<ProductResponse>(TestHelpers.JsonOptions);
        Assert.Equal(99, product!.StockQuantity);
    }
}

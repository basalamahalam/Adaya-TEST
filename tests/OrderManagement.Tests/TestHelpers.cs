using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests;

public static class TestHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> CreateProductAsync(HttpClient client, string name, int stock, decimal price = 1000)
    {
        var response = await client.PostAsJsonAsync("/products", new CreateProductRequest(name, stock, price), JsonOptions);
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return product!.Id;
    }

    public static Task<HttpResponseMessage> CreateOrderAsync(
        HttpClient client, string idempotencyKey, string customerId, int productId, int quantity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(
                customerId,
                [new CreateOrderItemRequest(productId, quantity)],
                "Jl. Test No. 1"), options: JsonOptions)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> UpdateStatusAsync(HttpClient client, int orderId, Api.Domain.OrderStatus status) =>
        client.PatchAsync($"/orders/{orderId}/status", JsonContent.Create(new UpdateOrderStatusRequest(status), options: JsonOptions));
}

using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Dtos;

public record CreateProductRequest(
    [Required, MinLength(1)] string Name,
    [Range(0, int.MaxValue)] int StockQuantity,
    [Range(0, double.MaxValue)] decimal Price
);

public record ProductResponse(int Id, string Name, int StockQuantity, decimal Price);

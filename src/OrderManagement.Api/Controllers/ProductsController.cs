using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Data;
using OrderManagement.Api.Domain;
using OrderManagement.Api.Dtos;
using OrderManagement.Api.Exceptions;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var product = new Product { Name = request.Name, StockQuantity = request.StockQuantity, Price = request.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        var response = ToResponse(product);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Get(int id, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct)
            ?? throw new NotFoundApiException($"Product {id} not found.");
        return Ok(ToResponse(product));
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> List(CancellationToken ct) =>
        Ok(await db.Products.Select(p => new ProductResponse(p.Id, p.Name, p.StockQuantity, p.Price)).ToListAsync(ct));

    private static ProductResponse ToResponse(Product p) => new(p.Id, p.Name, p.StockQuantity, p.Price);
}

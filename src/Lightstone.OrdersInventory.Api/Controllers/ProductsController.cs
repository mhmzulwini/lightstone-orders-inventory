using Lightstone.OrdersInventory.Api.Contracts;
using Lightstone.OrdersInventory.Api.Data;
using Lightstone.OrdersInventory.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lightstone.OrdersInventory.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(AppDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    // Read-only projections avoid entity tracking and return only public fields.
    public async Task<IReadOnlyCollection<ProductResponse>> GetAll(CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().OrderBy(x => x.Sku)
            .Select(x => new ProductResponse(x.Sku, x.Name, x.Price, x.AvailableStock))
            .ToArrayAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku)) return ValidationProblem("sku cannot be blank.");
        if (await db.Products.AnyAsync(x => x.Sku == sku, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Product already exists", Detail = $"Product '{sku}' already exists.", Status = 409 });

        var now = timeProvider.GetUtcNow();
        var product = new Product { Sku = sku, Name = request.Name.Trim(), Price = request.Price, AvailableStock = request.InitialStock, CreatedAt = now, UpdatedAt = now };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new ProductResponse(product.Sku, product.Name, product.Price, product.AvailableStock));
    }

    [HttpPost("{sku}/stock-adjustments")]
    public async Task<ActionResult<ProductResponse>> AdjustStock(string sku, AdjustStockRequest request, CancellationToken cancellationToken)
    {
        if (request.QuantityChange == 0) return ValidationProblem("quantity_change cannot be zero.");
        sku = sku.Trim().ToUpperInvariant();

        // The condition and adjustment execute as one SQL statement, preventing concurrent
        // stock adjustments from crossing below zero.
        var updated = await db.Products.Where(x => x.Sku == sku && x.AvailableStock + request.QuantityChange >= 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AvailableStock, x => x.AvailableStock + request.QuantityChange)
                .SetProperty(x => x.UpdatedAt, timeProvider.GetUtcNow()), cancellationToken);
        if (updated == 0)
        {
            if (!await db.Products.AnyAsync(x => x.Sku == sku, cancellationToken)) return NotFound();
            return Conflict(new ProblemDetails { Title = "Insufficient stock", Detail = "The adjustment would make stock negative.", Status = 409 });
        }

        var product = await db.Products.AsNoTracking().SingleAsync(x => x.Sku == sku, cancellationToken);
        return Ok(new ProductResponse(product.Sku, product.Name, product.Price, product.AvailableStock));
    }
}

using System.ComponentModel.DataAnnotations;

namespace Lightstone.OrdersInventory.Api.Contracts;

public sealed record CreateProductRequest(
    [Required, StringLength(64)] string Sku,
    [Required, StringLength(200)] string Name,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal Price,
    [Range(0, int.MaxValue)] int InitialStock);

public sealed record AdjustStockRequest([Range(int.MinValue, int.MaxValue)] int QuantityChange);

public sealed record ProductResponse(string Sku, string Name, decimal Price, int AvailableStock);

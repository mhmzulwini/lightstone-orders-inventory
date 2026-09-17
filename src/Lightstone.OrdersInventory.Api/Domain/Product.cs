namespace Lightstone.OrdersInventory.Api.Domain;

public sealed class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

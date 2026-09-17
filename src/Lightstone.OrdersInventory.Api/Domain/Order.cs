namespace Lightstone.OrdersInventory.Api.Domain;

public sealed class Order
{
    public long Id { get; set; }
    public required string ExternalOrderId { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public required string Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

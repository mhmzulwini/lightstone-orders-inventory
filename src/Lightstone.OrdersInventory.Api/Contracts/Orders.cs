using System.ComponentModel.DataAnnotations;

namespace Lightstone.OrdersInventory.Api.Contracts;

public sealed record SubmitOrderRequest(
    [Required, StringLength(100)] string ExternalOrderId,
    DateTimeOffset PlacedAt,
    [Required, MinLength(1)] IReadOnlyCollection<SubmitOrderItemRequest> Items);

public sealed record SubmitOrderItemRequest(
    [Required, StringLength(64)] string Sku,
    [Range(1, int.MaxValue)] int Qty,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal UnitPrice);

public sealed record OrderItemResponse(string Sku, int Qty, decimal UnitPrice, decimal GrossAmount);

public sealed record OrderResponse(
    long Id,
    string ExternalOrderId,
    DateTimeOffset PlacedAt,
    DateTimeOffset AcceptedAt,
    IReadOnlyCollection<OrderItemResponse> Items,
    decimal GrossAmount,
    bool IsDuplicate);

public enum SubmitOrderOutcome { Accepted, Duplicate, InsufficientStock, ProductNotFound, Invalid }

public sealed record SubmitOrderResult(SubmitOrderOutcome Outcome, OrderResponse? Order, string? Detail = null);

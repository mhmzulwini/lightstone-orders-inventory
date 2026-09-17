using Lightstone.OrdersInventory.Api.Contracts;
using Lightstone.OrdersInventory.Api.Data;
using Lightstone.OrdersInventory.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lightstone.OrdersInventory.Tests;

public sealed class OrderValidationTests
{
    [Fact]
    public async Task Rejects_non_utc_placed_at_before_accessing_the_database()
    {
        var result = await CreateService().SubmitAsync(new SubmitOrderRequest(
            "order-1",
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.FromHours(2)),
            [new SubmitOrderItemRequest("SKU-001", 1, 10m)]), CancellationToken.None);

        Assert.Equal(SubmitOrderOutcome.Invalid, result.Outcome);
        Assert.Contains("UTC", result.Detail);
    }

    [Fact]
    public async Task Rejects_duplicate_skus_before_accessing_the_database()
    {
        var result = await CreateService().SubmitAsync(new SubmitOrderRequest(
            "order-1",
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
            [new SubmitOrderItemRequest("sku-001", 1, 10m), new SubmitOrderItemRequest("SKU-001", 2, 10m)]), CancellationToken.None);

        Assert.Equal(SubmitOrderOutcome.Invalid, result.Outcome);
        Assert.Contains("only once", result.Detail);
    }

    private static OrderService CreateService()
    {
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        return new OrderService(context, TimeProvider.System, NullLogger<OrderService>.Instance);
    }
}

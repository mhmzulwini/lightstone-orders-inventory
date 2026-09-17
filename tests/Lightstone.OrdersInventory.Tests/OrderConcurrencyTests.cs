using Lightstone.OrdersInventory.Api.Contracts;
using Lightstone.OrdersInventory.Api.Data;
using Lightstone.OrdersInventory.Api.Domain;
using Lightstone.OrdersInventory.Api.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lightstone.OrdersInventory.Tests;

public sealed class OrderConcurrencyTests
{
    [SqlServerFact]
    public async Task Simultaneous_orders_cannot_oversell_inventory()
    {
        await using var database = await TestDatabase.CreateAsync(initialStock: 1);

        // Both services race for one unit using separate DbContext/SQL connections.
        var results = await Task.WhenAll(
            Submit(database.ConnectionString, "order-a"),
            Submit(database.ConnectionString, "order-b"));

        Assert.Single(results, x => x.Outcome == SubmitOrderOutcome.Accepted);
        Assert.Single(results, x => x.Outcome == SubmitOrderOutcome.InsufficientStock);
        await using var verification = database.CreateContext();
        Assert.Equal(0, await verification.Products.Select(x => x.AvailableStock).SingleAsync());
        Assert.Equal(1, await verification.Orders.CountAsync());
    }

    [SqlServerFact]
    public async Task Simultaneous_duplicate_submissions_only_decrement_stock_once()
    {
        await using var database = await TestDatabase.CreateAsync(initialStock: 2);

        // The unique external-order constraint must allow one winner and one replay.
        var results = await Task.WhenAll(
            Submit(database.ConnectionString, "same-external-id"),
            Submit(database.ConnectionString, "same-external-id"));

        Assert.Single(results, x => x.Outcome == SubmitOrderOutcome.Accepted);
        Assert.Single(results, x => x.Outcome == SubmitOrderOutcome.Duplicate);
        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Products.Select(x => x.AvailableStock).SingleAsync());
        Assert.Equal(1, await verification.Orders.CountAsync());
    }

    private static async Task<SubmitOrderResult> Submit(string connectionString, string externalOrderId)
    {
        await using var context = TestDatabase.CreateContext(connectionString);
        var service = new OrderService(context, TimeProvider.System, NullLogger<OrderService>.Instance);
        return await service.SubmitAsync(new SubmitOrderRequest(
            externalOrderId,
            new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
            [new SubmitOrderItemRequest("SKU-001", 1, 24.99m)]), CancellationToken.None);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(string connectionString) => ConnectionString = connectionString;
        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync(int initialStock)
        {
            var root = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")!;

            // Every test gets an isolated real SQL Server database. This exercises locking,
            // transactions, and unique constraints that an in-memory provider cannot model.
            var builder = new SqlConnectionStringBuilder(root) { InitialCatalog = $"OrdersInventoryTests_{Guid.NewGuid():N}" };
            var database = new TestDatabase(builder.ConnectionString);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow;
            context.Products.Add(new Product { Sku = "SKU-001", Name = "Mouse", Price = 24.99m, AvailableStock = initialStock, CreatedAt = now, UpdatedAt = now });
            await context.SaveChangesAsync();
            return database;
        }

        public AppDbContext CreateContext() => CreateContext(ConnectionString);
        public static AppDbContext CreateContext(string connectionString) =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);

        public async ValueTask DisposeAsync()
        {
            // Remove the isolated database after each test run.
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}

using Lightstone.OrdersInventory.Api.Contracts;
using Lightstone.OrdersInventory.Api.Data;
using Lightstone.OrdersInventory.Api.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lightstone.OrdersInventory.Api.Services;

public sealed class OrderService(AppDbContext db, TimeProvider timeProvider, ILogger<OrderService> logger)
{
    public async Task<SubmitOrderResult> SubmitAsync(SubmitOrderRequest request, CancellationToken cancellationToken)
    {
        // Normalizing SKUs once makes all later comparisons deterministic. Sorting also
        // makes concurrent multi-product orders acquire row locks in a consistent order.
        var externalOrderId = request.ExternalOrderId.Trim();
        var items = request.Items
            .Select(x => new { Sku = x.Sku.Trim().ToUpperInvariant(), x.Qty, x.UnitPrice })
            .OrderBy(x => x.Sku, StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(externalOrderId) || request.PlacedAt.Offset != TimeSpan.Zero)
            return new(SubmitOrderOutcome.Invalid, null, "external_order_id is required and placed_at must use UTC (Z).");

        if (items.Any(x => string.IsNullOrWhiteSpace(x.Sku)) || items.Select(x => x.Sku).Distinct().Count() != items.Length)
            return new(SubmitOrderOutcome.Invalid, null, "Each SKU must be present only once in an order.");

        logger.LogInformation("Order submission started for {ExternalOrderId} with {ItemCount} items", externalOrderId, items.Length);

        // Most retries can return immediately without intentionally causing a unique-key
        // exception. The database constraint below still handles simultaneous first attempts.
        var previouslyAccepted = await db.Orders.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.ExternalOrderId == externalOrderId, cancellationToken);
        if (previouslyAccepted is not null)
        {
            logger.LogInformation("Order submission was a duplicate for {ExternalOrderId}; original order {OrderId} returned", externalOrderId, previouslyAccepted.Id);
            return new(SubmitOrderOutcome.Duplicate, Map(previouslyAccepted, true));
        }

        // The order, every stock deduction, and every line item succeed or roll back together.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = new Order
        {
            ExternalOrderId = externalOrderId,
            PlacedAt = request.PlacedAt,
            AcceptedAt = timeProvider.GetUtcNow()
        };

        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // Another request inserted the same external ID between our initial lookup and
            // insert. Return its committed order without touching inventory a second time.
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var existing = await db.Orders.AsNoTracking().Include(x => x.Items)
                .SingleAsync(x => x.ExternalOrderId == externalOrderId, cancellationToken);
            logger.LogInformation("Order submission was a duplicate for {ExternalOrderId}; original order {OrderId} returned", externalOrderId, existing.Id);
            return new(SubmitOrderOutcome.Duplicate, Map(existing, true));
        }

        var skus = items.Select(x => x.Sku).ToArray();
        var products = await db.Products.Where(x => skus.Contains(x.Sku)).ToDictionaryAsync(x => x.Sku, cancellationToken);
        var missing = skus.FirstOrDefault(x => !products.ContainsKey(x));
        if (missing is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning("Order {ExternalOrderId} rejected because product {Sku} was not found", externalOrderId, missing);
            return new(SubmitOrderOutcome.ProductNotFound, null, $"Product '{missing}' was not found.");
        }

        foreach (var item in items)
        {
            var product = products[item.Sku];

            // This becomes one conditional UPDATE in SQL Server. Two requests cannot both
            // consume the same final units because SQL Server performs the check and change
            // atomically while locking the affected row.
            var updated = await db.Products
                .Where(x => x.Id == product.Id && x.AvailableStock >= item.Qty)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AvailableStock, x => x.AvailableStock - item.Qty)
                    .SetProperty(x => x.UpdatedAt, timeProvider.GetUtcNow()), cancellationToken);

            if (updated == 0)
            {
                // Rolling back also reverses deductions already made for earlier line items.
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning("Order {ExternalOrderId} rejected due to insufficient stock for {Sku}", externalOrderId, item.Sku);
                return new(SubmitOrderOutcome.InsufficientStock, null, $"Insufficient stock for product '{item.Sku}'.");
            }

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                // SKU and price are historical snapshots used by reporting.
                Sku = item.Sku,
                Quantity = item.Qty,
                UnitPrice = item.UnitPrice
            });
        }

        // Only after all products pass do we persist the lines and commit the order.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Order {ExternalOrderId} accepted as order {OrderId}", externalOrderId, order.Id);
        return new(SubmitOrderOutcome.Accepted, Map(order, false));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static OrderResponse Map(Order order, bool duplicate)
    {
        var items = order.Items.Select(x => new OrderItemResponse(
            x.Sku, x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToArray();
        return new(order.Id, order.ExternalOrderId, order.PlacedAt, order.AcceptedAt, items, items.Sum(x => x.GrossAmount), duplicate);
    }
}

using Lightstone.OrdersInventory.Api.Contracts;
using Lightstone.OrdersInventory.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lightstone.OrdersInventory.Api.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController(AppDbContext db) : ControllerBase
{
    [HttpGet("daily")]
    public async Task<ActionResult<SalesSummaryResponse>> GetDaily([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
    {
        if (endDate < startDate) return BadRequest(new ProblemDetails { Title = "Invalid date range", Detail = "end_date must be on or after start_date." });
        if (endDate.DayNumber - startDate.DayNumber > 366) return BadRequest(new ProblemDetails { Title = "Date range too large", Detail = "A maximum of 367 days can be requested." });

        // A half-open UTC interval avoids time-of-day rounding problems: the end date is
        // included by querying up to, but not including, midnight on the following day.
        var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endExclusive = new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Filtering and grouping are translated into SQL, so individual order lines are not
        // loaded into application memory as the dataset grows.
        var rows = await db.OrderItems.AsNoTracking()
            .Where(x => x.Order.PlacedAt >= start && x.Order.PlacedAt < endExclusive)
            .GroupBy(x => new { Date = x.Order.PlacedAt.Date, x.Sku })
            .Select(group => new { group.Key.Date, group.Key.Sku, Qty = group.Sum(x => x.Quantity), Gross = group.Sum(x => x.Quantity * x.UnitPrice) })
            .OrderBy(x => x.Date).ThenBy(x => x.Sku)
            .ToArrayAsync(cancellationToken);

        // The database has already reduced the result to one row per day and SKU. Combining
        // those small rows into the response's daily totals is inexpensive in memory.
        var days = rows.GroupBy(x => DateOnly.FromDateTime(x.Date))
            .Select(day =>
            {
                var products = day.Select(x => new ProductSalesResponse(x.Sku, x.Qty, x.Gross)).ToArray();
                return new DailySalesResponse(day.Key, products, new(products.Sum(x => x.QtySold), products.Sum(x => x.GrossSales)));
            }).ToArray();
        return Ok(new SalesSummaryResponse(startDate, endDate, days));
    }
}

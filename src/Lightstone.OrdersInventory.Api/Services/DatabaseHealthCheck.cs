using Lightstone.OrdersInventory.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lightstone.OrdersInventory.Api.Services;

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    // Readiness fails when SQL Server is unavailable, preventing traffic from being routed
    // to an API instance that cannot fulfil requests.
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("SQL Server is reachable.")
            : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
}

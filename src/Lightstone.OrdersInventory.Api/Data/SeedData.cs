using Lightstone.OrdersInventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lightstone.OrdersInventory.Api.Data;

public static class SeedData
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // MigrateAsync creates or upgrades the local database from committed migrations.
        await db.Database.MigrateAsync();

        // Seeding is idempotent: restarting the API does not duplicate demo products.
        if (await db.Products.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.Products.AddRange(
            new Product { Sku = "SKU-001", Name = "Wireless Mouse", Price = 24.99m, AvailableStock = 50, CreatedAt = now, UpdatedAt = now },
            new Product { Sku = "SKU-002", Name = "Mechanical Keyboard", Price = 89.00m, AvailableStock = 25, CreatedAt = now, UpdatedAt = now },
            new Product { Sku = "SKU-003", Name = "USB-C Hub", Price = 45.50m, AvailableStock = 30, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
    }
}

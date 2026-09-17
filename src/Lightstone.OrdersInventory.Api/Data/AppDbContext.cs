using Lightstone.OrdersInventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lightstone.OrdersInventory.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            // A SKU identifies one catalogue product regardless of API instance.
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.Property(x => x.Sku).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            // This final database guard prevents negative inventory even if data is
            // changed by another application or a manual SQL statement.
            entity.ToTable(t => t.HasCheckConstraint("CK_Products_AvailableStock", "[AvailableStock] >= 0"));
        });

        modelBuilder.Entity<Order>(entity =>
        {
            // The unique index is the authoritative idempotency guarantee. It protects
            // against simultaneous duplicates across multiple running API instances.
            entity.HasIndex(x => x.ExternalOrderId).IsUnique();
            // Daily reports filter orders by their placed date.
            entity.HasIndex(x => x.PlacedAt);
            entity.Property(x => x.ExternalOrderId).HasMaxLength(100);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(x => new { x.OrderId, x.ProductId });
            entity.HasIndex(x => x.Sku);
            entity.Property(x => x.Sku).HasMaxLength(64);
            // Order items retain the price paid even if the catalogue price changes.
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

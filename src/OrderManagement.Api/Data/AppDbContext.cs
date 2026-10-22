using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Domain;

namespace OrderManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Price).HasColumnType("decimal(18,2)");
            e.ToTable(t => t.HasCheckConstraint("CK_Products_StockQuantity_NonNegative", "StockQuantity >= 0"));
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.CustomerId).IsRequired().HasMaxLength(100);
            e.Property(o => o.ShippingAddress).IsRequired().HasMaxLength(500);
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CustomerId);
            e.HasIndex(o => o.CreatedAtUtc);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<IdempotencyKey>(e =>
        {
            e.Property(k => k.Key).IsRequired().HasMaxLength(200);
            e.HasIndex(k => k.Key).IsUnique();
        });
    }
}

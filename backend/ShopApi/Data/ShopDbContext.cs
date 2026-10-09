using Microsoft.EntityFrameworkCore;

namespace ShopApi.Data;

/// <summary>Maps the tables created by db/init.sql. The schema is not generated from here.</summary>
public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<ProductRow> Products => Set<ProductRow>();
    public DbSet<StockRow> Stock => Set<StockRow>();
    public DbSet<StockMovementRow> StockMovements => Set<StockMovementRow>();
    public DbSet<CartRow> Carts => Set<CartRow>();
    public DbSet<CartItemRow> CartItems => Set<CartItemRow>();
    public DbSet<OrderRow> Orders => Set<OrderRow>();
    public DbSet<OrderItemRow> OrderItems => Set<OrderItemRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ProductRow>(e =>
        {
            e.ToTable("products").HasKey(p => p.Code);
            e.Property(p => p.Code).HasColumnName("product_code");
            e.Property(p => p.Name).HasColumnName("product_name");
            e.Property(p => p.Price).HasColumnName("unit_price").HasPrecision(10, 2);
        });

        model.Entity<StockRow>(e =>
        {
            e.ToTable("stock").HasKey(s => s.Code);
            e.Property(s => s.Code).HasColumnName("product_code");
            e.Property(s => s.Quantity).HasColumnName("quantity");
        });

        model.Entity<StockMovementRow>(e =>
        {
            e.ToTable("stock_movements").HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("movement_id");
            e.Property(m => m.ProductCode).HasColumnName("product_code");
            e.Property(m => m.Quantity).HasColumnName("quantity");
            e.Property(m => m.Reference).HasColumnName("reference");
        });

        model.Entity<CartRow>(e =>
        {
            e.ToTable("carts").HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("cart_id");
        });

        model.Entity<CartItemRow>(e =>
        {
            e.ToTable("cart_items").HasKey(i => new { i.CartId, i.ProductCode });
            e.Property(i => i.CartId).HasColumnName("cart_id");
            e.Property(i => i.ProductCode).HasColumnName("product_code");
            e.Property(i => i.Quantity).HasColumnName("quantity");
        });

        model.Entity<OrderRow>(e =>
        {
            e.ToTable("orders").HasKey(o => o.Id);
            e.Property(o => o.Id).HasColumnName("order_id");
            e.Property(o => o.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        model.Entity<OrderItemRow>(e =>
        {
            e.ToTable("order_items").HasKey(i => new { i.OrderId, i.ProductCode });
            e.Property(i => i.OrderId).HasColumnName("order_id");
            e.Property(i => i.ProductCode).HasColumnName("product_code");
            e.Property(i => i.ProductName).HasColumnName("product_name");
            e.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(10, 2);
            e.Property(i => i.Quantity).HasColumnName("quantity");
        });
    }
}

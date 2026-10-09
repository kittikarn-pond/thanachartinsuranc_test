using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ShopApi.Models;

namespace ShopApi.Data;

public sealed class EfCartRepository(ShopDbContext db) : ICartRepository
{
    private const string LikeEscape = "\\";

    private class ProductWithStock
    {
        public ProductRow Product { get; set; } = null!;
        public StockRow Stock { get; set; } = null!;
    }

    // Products that have a stock row. "Available" sums the carts for just the rows that are returned
    // (a page, or a few codes), so it stays cheap however many products there are.
    private IQueryable<ProductWithStock> ProductsWithStock =>
        from p in db.Products
        join s in db.Stock on p.Code equals s.Code
        select new ProductWithStock { Product = p, Stock = s };

    private IQueryable<Product> ToProducts(IQueryable<ProductWithStock> rows) =>
        rows.OrderBy(x => x.Product.Code)
            .Select(x => new Product
            {
                ProductCode = x.Product.Code,
                ProductName = x.Product.Name,
                UnitPrice = x.Product.Price,
                Quantity = x.Stock.Quantity,
                Available = x.Stock.Quantity
                            - (db.CartItems.Where(c => c.ProductCode == x.Product.Code).Sum(c => (int?)c.Quantity) ?? 0),
            });

    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? search, string? after, int limit)
    {
        var rows = ProductsWithStock;
        if (after is not null)
            rows = rows.Where(x => x.Product.Code.CompareTo(after) > 0);
        if (search is not null)
        {
            // prefix match, so the indexes on product_code and product_name are used
            var pattern = EscapeLike(search) + "%";
            rows = rows.Where(x => EF.Functions.Like(x.Product.Code, pattern, LikeEscape)
                                   || EF.Functions.Like(x.Product.Name, pattern, LikeEscape));
        }

        return await ToProducts(rows.OrderBy(x => x.Product.Code).Take(limit)).ToListAsync();
    }

    public async Task<IReadOnlyList<Product>> GetProductsByCodesAsync(IReadOnlyCollection<string> codes)
    {
        var wanted = codes.ToList();
        return await ToProducts(ProductsWithStock.Where(x => wanted.Contains(x.Product.Code))).ToListAsync();
    }

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    public async Task<Cart> GetCartAsync(Guid cartId)
    {
        var id = cartId.ToString();
        var lines = await (
            from i in db.CartItems
            where i.CartId == id
            join p in db.Products on i.ProductCode equals p.Code
            join s in db.Stock on i.ProductCode equals s.Code
            orderby i.ProductCode
            select new CartLine
            {
                ProductCode = i.ProductCode,
                ProductName = p.Name,
                UnitPrice = p.Price,
                Quantity = i.Quantity,
                Available = s.Quantity - db.CartItems.Where(o => o.ProductCode == i.ProductCode).Sum(o => o.Quantity),
            }).ToListAsync();
        return new Cart(lines);
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work)
    {
        // Leaving the block without Commit rolls back, which is what happens when work throws.
        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await work();
        await tx.CommitAsync();
        return result;
    }

    // LINQ cannot ask for a row lock, so this is the one raw SQL statement. The value comes from the locking
    // read itself: a normal read inside the transaction could return an older snapshot.
    public async Task<int?> LockStockAsync(string productCode)
    {
        var rows = await db.Database
            .SqlQuery<int>($"SELECT quantity AS Value FROM stock WHERE product_code = {productCode} FOR UPDATE")
            .ToListAsync();
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<int> GetReservedAsync(string productCode) =>
        await db.CartItems.Where(i => i.ProductCode == productCode).SumAsync(i => (int?)i.Quantity) ?? 0;

    public async Task<int> GetCartQuantityAsync(Guid cartId, string productCode)
    {
        var id = cartId.ToString();
        return await db.CartItems.Where(i => i.CartId == id && i.ProductCode == productCode)
            .SumAsync(i => (int?)i.Quantity) ?? 0;
    }

    public async Task SetCartQuantityAsync(Guid cartId, string productCode, int quantity)
    {
        var id = cartId.ToString();
        var line = db.CartItems.Where(i => i.CartId == id && i.ProductCode == productCode);
        if (quantity == 0)
        {
            await line.ExecuteDeleteAsync();
            return;
        }

        await EnsureCartAsync(id);
        var updated = await line.ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, quantity));
        if (updated == 0)
        {
            db.CartItems.Add(new CartItemRow { CartId = id, ProductCode = productCode, Quantity = quantity });
            await db.SaveChangesAsync();
        }
    }

    private async Task EnsureCartAsync(string id)
    {
        if (await db.Carts.AnyAsync(c => c.Id == id)) return;

        db.Carts.Add(new CartRow { Id = id });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            db.ChangeTracker.Clear();   // another request created the cart a moment ago
        }
    }

    public async Task ClearCartAsync(Guid cartId)
    {
        var id = cartId.ToString();
        await db.CartItems.Where(i => i.CartId == id).ExecuteDeleteAsync();
    }

    public Task DeductStockAsync(string productCode, int quantity) =>
        db.Stock.Where(s => s.Code == productCode)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity - quantity));

    public Task AddStockAsync(string productCode, int quantity) =>
        db.Stock.Where(s => s.Code == productCode)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + quantity));

    public async Task RecordMovementAsync(string productCode, int quantity, string? reference)
    {
        db.StockMovements.Add(new StockMovementRow { ProductCode = productCode, Quantity = quantity, Reference = reference });
        await db.SaveChangesAsync();
    }

    public async Task<long> CreateOrderAsync(IReadOnlyList<CartLine> lines, decimal total)
    {
        var order = new OrderRow
        {
            TotalAmount = total,
            Items = lines.Select(l => new OrderItemRow
            {
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
            }).ToList(),
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}

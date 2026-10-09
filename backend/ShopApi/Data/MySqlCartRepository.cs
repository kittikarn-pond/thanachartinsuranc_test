using System.Data.Common;
using Dapper;
using MySqlConnector;
using ShopApi.Models;

namespace ShopApi.Data;

public sealed class MySqlCartRepository(IConfiguration config) : ICartRepository, IAsyncDisposable
{
    private readonly MySqlConnection _db = new(config.GetConnectionString("Default"));
    private DbTransaction? _tx;

    // "Available" sums the carts for just the rows returned (a page, or a few codes), using the product_code
    // index on cart_items, so it stays cheap however many products there are.
    private const string ProductSelect = """
        SELECT p.product_code AS ProductCode, p.product_name AS ProductName, p.unit_price AS UnitPrice,
               s.quantity AS Quantity,
               CAST(s.quantity - COALESCE((SELECT SUM(ci.quantity) FROM cart_items ci
                                           WHERE ci.product_code = p.product_code), 0) AS SIGNED) AS Available
        FROM products p
        JOIN stock s ON s.product_code = p.product_code
        """;

    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? search, string? after, int limit)
    {
        var where = new List<string>();
        var args = new DynamicParameters();
        args.Add("limit", limit);

        if (after is not null)
        {
            where.Add("p.product_code > @after");
            args.Add("after", after);
        }
        if (search is not null)
        {
            // prefix match, so the indexes on product_code and product_name are used
            where.Add(@"(p.product_code LIKE @pattern ESCAPE '\\' OR p.product_name LIKE @pattern ESCAPE '\\')");
            args.Add("pattern", EscapeLike(search) + "%");
        }

        var sql = ProductSelect
                  + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
                  + " ORDER BY p.product_code LIMIT @limit";
        return (await _db.QueryAsync<Product>(sql, args)).ToList();
    }

    public async Task<IReadOnlyList<Product>> GetProductsByCodesAsync(IReadOnlyCollection<string> codes) =>
        codes.Count == 0
            ? []
            : (await _db.QueryAsync<Product>(
                ProductSelect + " WHERE p.product_code IN @codes ORDER BY p.product_code",
                new { codes }, _tx)).ToList();

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    public async Task<Cart> GetCartAsync(Guid cartId)
    {
        var lines = await _db.QueryAsync<CartLine>("""
            SELECT ci.product_code AS ProductCode, p.product_name AS ProductName,
                   p.unit_price AS UnitPrice, ci.quantity AS Quantity,
                   CAST(s.quantity - (SELECT SUM(o.quantity) FROM cart_items o
                                      WHERE o.product_code = ci.product_code) AS SIGNED) AS Available
            FROM cart_items ci
            JOIN products p ON p.product_code = ci.product_code
            JOIN stock s ON s.product_code = ci.product_code
            WHERE ci.cart_id = @id
            ORDER BY ci.product_code
            """, new { id = cartId.ToString() }, _tx);
        return new Cart(lines.ToList());
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work)
    {
        await _db.OpenAsync();
        _tx = await _db.BeginTransactionAsync();
        try
        {
            var result = await work();
            await _tx.CommitAsync();
            return result;
        }
        catch
        {
            await _tx.RollbackAsync();
            throw;
        }
        finally
        {
            await _tx.DisposeAsync();
            _tx = null;
        }
    }

    public Task<int?> LockStockAsync(string productCode) =>
        _db.ExecuteScalarAsync<int?>(
            "SELECT quantity FROM stock WHERE product_code = @productCode FOR UPDATE", new { productCode }, _tx);

    public Task<int> GetReservedAsync(string productCode) =>
        _db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(SUM(quantity), 0) FROM cart_items WHERE product_code = @productCode", new { productCode }, _tx);

    public Task<int> GetCartQuantityAsync(Guid cartId, string productCode) =>
        _db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(SUM(quantity), 0) FROM cart_items WHERE cart_id = @id AND product_code = @productCode",
            new { id = cartId.ToString(), productCode }, _tx);

    public async Task SetCartQuantityAsync(Guid cartId, string productCode, int quantity)
    {
        var id = cartId.ToString();
        if (quantity == 0)
        {
            await _db.ExecuteAsync(
                "DELETE FROM cart_items WHERE cart_id = @id AND product_code = @productCode", new { id, productCode }, _tx);
            return;
        }
        await _db.ExecuteAsync("INSERT IGNORE INTO carts (cart_id) VALUES (@id)", new { id }, _tx);
        await _db.ExecuteAsync("""
            INSERT INTO cart_items (cart_id, product_code, quantity) VALUES (@id, @productCode, @quantity)
            ON DUPLICATE KEY UPDATE quantity = @quantity
            """, new { id, productCode, quantity }, _tx);
    }

    public Task ClearCartAsync(Guid cartId) =>
        _db.ExecuteAsync("DELETE FROM cart_items WHERE cart_id = @id", new { id = cartId.ToString() }, _tx);

    public Task DeductStockAsync(string productCode, int quantity) =>
        _db.ExecuteAsync(
            "UPDATE stock SET quantity = quantity - @quantity WHERE product_code = @productCode",
            new { productCode, quantity }, _tx);

    public Task AddStockAsync(string productCode, int quantity) =>
        _db.ExecuteAsync(
            "UPDATE stock SET quantity = quantity + @quantity WHERE product_code = @productCode",
            new { productCode, quantity }, _tx);

    public Task RecordMovementAsync(string productCode, int quantity, string? reference) =>
        _db.ExecuteAsync(
            "INSERT INTO stock_movements (product_code, quantity, reference) VALUES (@productCode, @quantity, @reference)",
            new { productCode, quantity, reference }, _tx);

    public async Task<long> CreateOrderAsync(IReadOnlyList<CartLine> lines, decimal total)
    {
        var orderId = await _db.ExecuteScalarAsync<long>(
            "INSERT INTO orders (total_amount) VALUES (@total); SELECT LAST_INSERT_ID();", new { total }, _tx);
        foreach (var l in lines)
            await _db.ExecuteAsync("""
                INSERT INTO order_items (order_id, product_code, product_name, unit_price, quantity)
                VALUES (@orderId, @ProductCode, @ProductName, @UnitPrice, @Quantity)
                """, new { orderId, l.ProductCode, l.ProductName, l.UnitPrice, l.Quantity }, _tx);
        return orderId;
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}

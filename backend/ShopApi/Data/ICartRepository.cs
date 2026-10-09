using ShopApi.Models;

namespace ShopApi.Data;

/// <summary>Data access for products, stock, carts and orders.</summary>
public interface ICartRepository
{
    /// <summary>
    /// Up to <paramref name="limit"/> products ordered by code, only those after code <paramref name="after"/>,
    /// and only those whose code or name starts with <paramref name="search"/> (when given).
    /// </summary>
    Task<IReadOnlyList<Product>> GetProductsAsync(string? search, string? after, int limit);

    /// <summary>The products with these codes, ordered by code. Unknown codes are skipped.</summary>
    Task<IReadOnlyList<Product>> GetProductsByCodesAsync(IReadOnlyCollection<string> codes);

    Task<Cart> GetCartAsync(Guid cartId);

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction. Commits when it returns,
    /// rolls back when it throws. The methods below must be called inside it.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work);

    /// <summary>Locks the product's stock row and returns the on-hand quantity, or null if the product does not exist.</summary>
    Task<int?> LockStockAsync(string productCode);

    /// <summary>Quantity of the product held in all carts.</summary>
    Task<int> GetReservedAsync(string productCode);

    /// <summary>Quantity of the product held in this cart.</summary>
    Task<int> GetCartQuantityAsync(Guid cartId, string productCode);

    /// <summary>Sets the quantity of a cart line (creates the cart if needed). 0 removes the line.</summary>
    Task SetCartQuantityAsync(Guid cartId, string productCode, int quantity);

    Task ClearCartAsync(Guid cartId);
    Task DeductStockAsync(string productCode, int quantity);

    /// <summary>Adds to the on-hand quantity of a product.</summary>
    Task AddStockAsync(string productCode, int quantity);

    /// <summary>Writes a history row for stock that came in.</summary>
    Task RecordMovementAsync(string productCode, int quantity, string? reference);
    Task<long> CreateOrderAsync(IReadOnlyList<CartLine> lines, decimal total);
}

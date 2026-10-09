using ShopApi.Data;
using ShopApi.Models;

namespace ShopApi.Tests;

/// <summary>In-memory repository so the cart rules can be tested without a database.</summary>
public class FakeCartRepository : ICartRepository
{
    private record Item(string Code, string Name, decimal Price);

    // Codes compare case-insensitively, like the database column does.
    private readonly Dictionary<string, Item> _items = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _stock = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<Guid, Dictionary<string, int>> _carts = new();

    public List<Receipt> Orders { get; } = [];
    public List<StockMovement> Movements { get; } = [];

    public FakeCartRepository AddProduct(string code, string name, decimal price, int stock)
    {
        _items[code] = new Item(code, name, price);
        _stock[code] = stock;
        return this;
    }

    public int OnHand(string code) => _stock[code];

    /// <summary>Codes passed to LockStockAsync, in order.</summary>
    public List<string> LockedCodes { get; } = [];

    // Same rules as the SQL: ordered by code, case-insensitive, search is a prefix of the code or the name.
    public Task<IReadOnlyList<Product>> GetProductsAsync(string? search, string? after, int limit) =>
        Task.FromResult<IReadOnlyList<Product>>(_items
            .Where(kv => after is null || StringComparer.OrdinalIgnoreCase.Compare(kv.Key, after) > 0)
            .Where(kv => search is null
                         || kv.Key.StartsWith(search, StringComparison.OrdinalIgnoreCase)
                         || kv.Value.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(kv => ToProduct(kv.Key))
            .ToList());

    public Task<IReadOnlyList<Product>> GetProductsByCodesAsync(IReadOnlyCollection<string> codes) =>
        Task.FromResult<IReadOnlyList<Product>>(codes
            .Where(_items.ContainsKey)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(ToProduct)
            .ToList());

    private Product ToProduct(string code) => new()
    {
        ProductCode = _items[code].Code,
        ProductName = _items[code].Name,
        UnitPrice = _items[code].Price,
        Quantity = _stock[code],
        Available = _stock[code] - Reserved(code),
    };

    public Task<Cart> GetCartAsync(Guid cartId)
    {
        var lines = _carts.GetValueOrDefault(cartId, [])
            .OrderBy(kv => kv.Key)
            .Select(kv => new CartLine
            {
                ProductCode = kv.Key,
                ProductName = _items[kv.Key].Name,
                UnitPrice = _items[kv.Key].Price,
                Quantity = kv.Value,
                Available = _stock[kv.Key] - Reserved(kv.Key),
            }).ToList();
        return Task.FromResult(new Cart(lines));
    }

    // Like a real transaction: if the work throws, everything it changed is undone.
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work)
    {
        var stock = new Dictionary<string, int>(_stock, _stock.Comparer);
        var carts = _carts.ToDictionary(c => c.Key, c => new Dictionary<string, int>(c.Value, c.Value.Comparer));
        var orders = Orders.Count;
        var movements = Movements.Count;
        try
        {
            return await work();
        }
        catch
        {
            _stock = stock;
            _carts = carts;
            Orders.RemoveRange(orders, Orders.Count - orders);
            Movements.RemoveRange(movements, Movements.Count - movements);
            throw;
        }
    }

    public Task<int?> LockStockAsync(string productCode)
    {
        LockedCodes.Add(productCode);
        return Task.FromResult(_stock.TryGetValue(productCode, out var q) ? (int?)q : null);
    }

    public Task<int> GetReservedAsync(string productCode) => Task.FromResult(Reserved(productCode));

    public Task<int> GetCartQuantityAsync(Guid cartId, string productCode) =>
        Task.FromResult(_carts.GetValueOrDefault(cartId)?.GetValueOrDefault(productCode) ?? 0);

    public Task SetCartQuantityAsync(Guid cartId, string productCode, int quantity)
    {
        if (!_carts.TryGetValue(cartId, out var cart)) _carts[cartId] = cart = new(StringComparer.OrdinalIgnoreCase);
        if (quantity == 0) cart.Remove(productCode);
        else cart[productCode] = quantity;
        return Task.CompletedTask;
    }

    public Task ClearCartAsync(Guid cartId)
    {
        _carts.Remove(cartId);
        return Task.CompletedTask;
    }

    public Task DeductStockAsync(string productCode, int quantity)
    {
        _stock[productCode] -= quantity;
        return Task.CompletedTask;
    }

    public Task AddStockAsync(string productCode, int quantity)
    {
        _stock[productCode] += quantity;
        return Task.CompletedTask;
    }

    public Task RecordMovementAsync(string productCode, int quantity, string? reference)
    {
        Movements.Add(new StockMovement(productCode, quantity, reference));
        return Task.CompletedTask;
    }

    public Task<long> CreateOrderAsync(IReadOnlyList<CartLine> lines, decimal total)
    {
        var id = Orders.Count + 1;
        Orders.Add(new Receipt(id, lines, total));
        return Task.FromResult((long)id);
    }

    private int Reserved(string code) => _carts.Values.Sum(c => c.GetValueOrDefault(code));
}

using ShopApi.Data;
using ShopApi.Models;

namespace ShopApi.Services;

/// <summary>Reading the product list. Never loads everything: callers get a page, or the few codes they ask for.</summary>
public class ProductService(ICartRepository repo)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;
    public const int MaxCodes = 200;
    public const int MaxSearchLength = 100;

    /// <summary>
    /// Products ordered by code. Pass the previous page's Next as <paramref name="after"/> to get the following page.
    /// <paramref name="search"/> keeps products whose code or name starts with it.
    /// </summary>
    public async Task<ProductPage> GetPageAsync(string? search, string? after, int? limit)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
            throw new ShopException(ShopError.Invalid, $"Search must be {MaxSearchLength} characters or fewer");

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        // ask for one extra row: if it comes back, there is a next page
        var rows = await repo.GetProductsAsync(search, string.IsNullOrEmpty(after) ? null : after, take + 1);
        var items = rows.Take(take).ToList();
        return new ProductPage(items, rows.Count > take ? items[^1].ProductCode : null);
    }

    /// <summary>Fresh numbers for specific products (the frontend uses this to refresh only the cards that changed).</summary>
    public Task<IReadOnlyList<Product>> GetByCodesAsync(IReadOnlyCollection<string> codes)
    {
        var distinct = codes.Distinct().ToList();
        if (distinct.Count > MaxCodes)
            throw new ShopException(ShopError.Invalid, $"At most {MaxCodes} codes at a time");
        return repo.GetProductsByCodesAsync(distinct);
    }
}

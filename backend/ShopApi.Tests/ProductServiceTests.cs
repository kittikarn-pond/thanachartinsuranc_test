using ShopApi.Models;
using ShopApi.Services;

namespace ShopApi.Tests;

public class ProductServiceTests
{
    private readonly FakeCartRepository _repo = new();
    private readonly ProductService _service;

    public ProductServiceTests() => _service = new ProductService(_repo);

    private ProductServiceTests Products(int count)
    {
        for (var i = 1; i <= count; i++)
            _repo.AddProduct($"P{i:000}", $"Item {i}", 1m, stock: 10);
        return this;
    }

    [Fact]
    public async Task Pages_cover_every_product_once_in_code_order()
    {
        Products(7);
        var seen = new List<string>();
        string? after = null;
        var pages = 0;

        do
        {
            var page = await _service.GetPageAsync(null, after, limit: 3);
            seen.AddRange(page.Items.Select(p => p.ProductCode));
            after = page.Next;
            pages++;
        } while (after is not null);

        Assert.Equal(3, pages);
        Assert.Equal(Enumerable.Range(1, 7).Select(i => $"P{i:000}"), seen);
    }

    [Fact]
    public async Task Last_page_has_no_next()
    {
        Products(3);

        var page = await _service.GetPageAsync(null, null, limit: 3);

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.Next);
    }

    [Fact]
    public async Task Empty_catalogue_gives_an_empty_page()
    {
        var page = await _service.GetPageAsync(null, null, null);

        Assert.Empty(page.Items);
        Assert.Null(page.Next);
    }

    [Theory]
    [InlineData(null, ProductService.DefaultLimit)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100_000, ProductService.MaxLimit)]
    public async Task Limit_is_clamped(int? asked, int expected)
    {
        Products(ProductService.MaxLimit + 20);

        var page = await _service.GetPageAsync(null, null, asked);

        Assert.Equal(expected, page.Items.Count);
    }

    [Fact]
    public async Task Search_matches_the_start_of_the_code_or_the_name_ignoring_case()
    {
        _repo.AddProduct("A1", "Soap bar", 1m, 5)
             .AddProduct("B2", "Shampoo", 1m, 5)
             .AddProduct("SOAP9", "Other", 1m, 5)
             .AddProduct("C3", "Dish soap", 1m, 5);   // contains "soap" but does not start with it

        var page = await _service.GetPageAsync("  soap ", null, null);

        Assert.Equal(["A1", "SOAP9"], page.Items.Select(p => p.ProductCode));
    }

    [Fact]
    public async Task Search_pages_continue_where_the_last_one_stopped()
    {
        Products(5);

        var first = await _service.GetPageAsync("item", null, limit: 2);
        var second = await _service.GetPageAsync("item", first.Next, limit: 2);

        Assert.Equal(["P001", "P002"], first.Items.Select(p => p.ProductCode));
        Assert.Equal(["P003", "P004"], second.Items.Select(p => p.ProductCode));
    }

    [Fact]
    public async Task Blank_search_means_no_filter()
    {
        Products(3);

        var page = await _service.GetPageAsync("   ", null, null);

        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task Too_long_search_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(
            () => _service.GetPageAsync(new string('x', ProductService.MaxSearchLength + 1), null, null));

        Assert.Equal(ShopError.Invalid, ex.Error);
    }

    [Fact]
    public async Task Page_shows_what_is_available_after_carts_hold_some()
    {
        Products(1);
        await new CartService(_repo).AddItemAsync(Guid.NewGuid(), "P001", 4);

        var item = (await _service.GetPageAsync(null, null, null)).Items.Single();

        Assert.Equal(10, item.Quantity);
        Assert.Equal(6, item.Available);
    }

    [Fact]
    public async Task By_codes_returns_only_those_products_and_skips_unknown_codes()
    {
        Products(5);

        var found = await _service.GetByCodesAsync(["P004", "NOPE", "P002", "P004"]);

        Assert.Equal(["P002", "P004"], found.Select(p => p.ProductCode));
    }

    [Fact]
    public async Task By_codes_with_nothing_asked_returns_nothing()
    {
        Products(2);

        Assert.Empty(await _service.GetByCodesAsync([]));
    }

    [Fact]
    public async Task By_codes_rejects_too_many_codes()
    {
        var codes = Enumerable.Range(0, ProductService.MaxCodes + 1).Select(i => $"C{i}").ToList();

        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.GetByCodesAsync(codes));

        Assert.Equal(ShopError.Invalid, ex.Error);
    }

    [Fact]
    public async Task Cart_lines_say_how_many_more_can_be_added()
    {
        Products(1);
        var cartService = new CartService(_repo);
        var mine = Guid.NewGuid();
        await cartService.AddItemAsync(Guid.NewGuid(), "P001", 3);   // someone else holds 3

        var cart = await cartService.AddItemAsync(mine, "P001", 2);

        Assert.Equal(5, cart.Items.Single().Available);   // 10 in stock - 3 - 2
    }
}

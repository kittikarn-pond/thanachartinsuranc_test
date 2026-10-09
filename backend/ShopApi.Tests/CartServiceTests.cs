using ShopApi.Models;
using ShopApi.Services;

namespace ShopApi.Tests;

public class CartServiceTests
{
    private readonly FakeCartRepository _repo = new FakeCartRepository()
        .AddProduct("P1", "Water", 10m, stock: 5)
        .AddProduct("P2", "Soap", 25m, stock: 3);
    private readonly CartService _service;
    private readonly Guid _cart = Guid.NewGuid();

    public CartServiceTests() => _service = new CartService(_repo);

    private async Task<int> AvailableAsync(string code) =>
        (await _repo.GetProductsByCodesAsync([code])).Single().Available;

    [Fact]
    public async Task Add_puts_item_in_cart_and_lowers_available_but_not_on_hand()
    {
        var cart = await _service.AddItemAsync(_cart, "P1", 2);

        Assert.Equal(2, cart.Items.Single().Quantity);
        Assert.Equal(20m, cart.Total);
        Assert.Equal(3, await AvailableAsync("P1"));
        Assert.Equal(5, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task Add_same_product_twice_adds_up()
    {
        await _service.AddItemAsync(_cart, "P1", 2);
        var cart = await _service.AddItemAsync(_cart, "P1", 1);

        Assert.Equal(3, cart.Items.Single().Quantity);
    }

    [Fact]
    public async Task Add_more_than_available_is_refused_and_cart_unchanged()
    {
        await _service.AddItemAsync(_cart, "P2", 2);

        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.AddItemAsync(_cart, "P2", 2));

        Assert.Equal(ShopError.OutOfStock, ex.Error);
        Assert.Equal(2, (await _service.GetCartAsync(_cart)).Items.Single().Quantity);
    }

    [Fact]
    public async Task Add_can_take_exactly_the_last_item()
    {
        await _service.AddItemAsync(_cart, "P2", 3);

        Assert.Equal(0, await AvailableAsync("P2"));
    }

    [Fact]
    public async Task Stock_held_by_another_cart_is_not_available()
    {
        await _service.AddItemAsync(Guid.NewGuid(), "P2", 3);

        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.AddItemAsync(_cart, "P2", 1));

        Assert.Equal(ShopError.OutOfStock, ex.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Add_with_zero_or_negative_quantity_is_invalid(int quantity)
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.AddItemAsync(_cart, "P1", quantity));

        Assert.Equal(ShopError.Invalid, ex.Error);
    }

    [Fact]
    public async Task Add_unknown_product_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.AddItemAsync(_cart, "NOPE", 1));

        Assert.Equal(ShopError.NotFound, ex.Error);
    }

    [Fact]
    public async Task Decrease_gives_stock_back()
    {
        await _service.AddItemAsync(_cart, "P1", 4);

        var cart = await _service.SetQuantityAsync(_cart, "P1", 1);

        Assert.Equal(1, cart.Items.Single().Quantity);
        Assert.Equal(4, await AvailableAsync("P1"));
    }

    [Fact]
    public async Task Set_to_zero_removes_the_line()
    {
        await _service.AddItemAsync(_cart, "P1", 2);

        var cart = await _service.SetQuantityAsync(_cart, "P1", 0);

        Assert.Empty(cart.Items);
        Assert.Equal(5, await AvailableAsync("P1"));
    }

    [Fact]
    public async Task Increase_up_to_available_works_and_beyond_is_refused()
    {
        await _service.AddItemAsync(_cart, "P2", 1);

        var cart = await _service.SetQuantityAsync(_cart, "P2", 3);
        Assert.Equal(3, cart.Items.Single().Quantity);

        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.SetQuantityAsync(_cart, "P2", 4));
        Assert.Equal(ShopError.OutOfStock, ex.Error);
        Assert.Equal(3, (await _service.GetCartAsync(_cart)).Items.Single().Quantity);
    }

    [Fact]
    public async Task Set_negative_quantity_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.SetQuantityAsync(_cart, "P1", -1));

        Assert.Equal(ShopError.Invalid, ex.Error);
    }

    [Fact]
    public async Task Remove_item_gives_stock_back_and_keeps_other_items()
    {
        await _service.AddItemAsync(_cart, "P1", 2);
        await _service.AddItemAsync(_cart, "P2", 1);

        var cart = await _service.RemoveItemAsync(_cart, "P1");

        Assert.Equal("P2", cart.Items.Single().ProductCode);
        Assert.Equal(5, await AvailableAsync("P1"));
    }

    [Fact]
    public async Task Clear_empties_cart_and_gives_all_stock_back()
    {
        await _service.AddItemAsync(_cart, "P1", 2);
        await _service.AddItemAsync(_cart, "P2", 3);

        var cart = await _service.ClearAsync(_cart);

        Assert.Empty(cart.Items);
        Assert.Equal(5, await AvailableAsync("P1"));
        Assert.Equal(3, await AvailableAsync("P2"));
    }

    [Fact]
    public async Task Checkout_returns_total_deducts_on_hand_stock_and_empties_cart()
    {
        await _service.AddItemAsync(_cart, "P1", 2);   // 2 x 10
        await _service.AddItemAsync(_cart, "P2", 1);   // 1 x 25

        var receipt = await _service.CheckoutAsync(_cart);

        Assert.Equal(45m, receipt.Total);
        Assert.Equal(2, receipt.Items.Count);
        Assert.Equal(3, _repo.OnHand("P1"));
        Assert.Equal(2, _repo.OnHand("P2"));
        Assert.Equal(3, await AvailableAsync("P1"));
        Assert.Empty((await _service.GetCartAsync(_cart)).Items);
        Assert.Single(_repo.Orders);
    }

    [Fact]
    public async Task Checkout_empty_cart_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.CheckoutAsync(_cart));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Empty(_repo.Orders);
    }

    [Fact]
    public async Task Checkout_changes_nothing_when_any_line_lacks_stock()
    {
        await _service.AddItemAsync(_cart, "P1", 2);
        await _service.AddItemAsync(_cart, "P2", 3);
        _repo.AddProduct("P2", "Soap", 25m, stock: 1);   // stock dropped after the items were added

        var ex = await Assert.ThrowsAsync<ShopException>(() => _service.CheckoutAsync(_cart));

        Assert.Equal(ShopError.OutOfStock, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
        Assert.Equal(1, _repo.OnHand("P2"));
        Assert.Equal(2, (await _service.GetCartAsync(_cart)).Items.Count);
        Assert.Empty(_repo.Orders);
    }
}

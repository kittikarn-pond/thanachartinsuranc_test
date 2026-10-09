using ShopApi.Models;
using ShopApi.Services;

namespace ShopApi.Tests;

public class StockServiceTests
{
    private readonly FakeCartRepository _repo = new FakeCartRepository()
        .AddProduct("P1", "Water", 10m, stock: 5)
        .AddProduct("P2", "Soap", 25m, stock: 3);
    private readonly StockService _stock;
    private readonly CartService _carts;

    public StockServiceTests()
    {
        _stock = new StockService(_repo);
        _carts = new CartService(_repo);
    }

    [Fact]
    public async Task AddStock_raises_on_hand_and_available()
    {
        var product = await _stock.AddStockAsync("P1", 20);

        Assert.Equal(25, product.Quantity);
        Assert.Equal(25, product.Available);
        Assert.Equal(25, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task AddStock_keeps_what_carts_hold()
    {
        var cartId = Guid.NewGuid();
        await _carts.AddItemAsync(cartId, "P1", 4);

        var product = await _stock.AddStockAsync("P1", 10);

        Assert.Equal(15, product.Quantity);
        Assert.Equal(11, product.Available);
        Assert.Equal(4, (await _carts.GetCartAsync(cartId)).Items.Single().Quantity);
    }

    [Fact]
    public async Task AddStock_lets_a_sold_out_product_be_added_to_a_cart_again()
    {
        var cartId = Guid.NewGuid();
        await _carts.AddItemAsync(cartId, "P2", 3);
        await _stock.AddStockAsync("P2", 2);

        var cart = await _carts.AddItemAsync(cartId, "P2", 2);

        Assert.Equal(5, cart.Items.Single().Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddStock_with_zero_or_negative_quantity_is_invalid(int quantity)
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _stock.AddStockAsync("P1", quantity));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task AddStock_unknown_product_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _stock.AddStockAsync("NOPE", 1));

        Assert.Equal(ShopError.NotFound, ex.Error);
    }

    [Fact]
    public async Task AddStock_beyond_the_maximum_is_invalid_and_changes_nothing()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _stock.AddStockAsync("P1", int.MaxValue));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task AddStock_can_reach_exactly_the_maximum()
    {
        var product = await _stock.AddStockAsync("P1", int.MaxValue - 5);

        Assert.Equal(int.MaxValue, product.Quantity);
    }

    [Fact]
    public async Task AddStock_records_a_movement_with_the_reference()
    {
        await _stock.AddStockAsync("P1", 20, "PO-1001");

        var movement = Assert.Single(_repo.Movements);
        Assert.Equal(new StockMovement("P1", 20, "PO-1001"), movement);
    }

    [Fact]
    public async Task AddStock_reference_is_optional_and_blank_becomes_null()
    {
        await _stock.AddStockAsync("P1", 1);
        await _stock.AddStockAsync("P1", 1, "   ");

        Assert.All(_repo.Movements, m => Assert.Null(m.Reference));
    }

    [Fact]
    public async Task AddStock_reference_longer_than_200_is_invalid_and_changes_nothing()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _stock.AddStockAsync("P1", 1, new string('x', 201)));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
        Assert.Empty(_repo.Movements);
    }

    [Fact]
    public async Task Failed_AddStock_records_no_movement()
    {
        await Assert.ThrowsAsync<ShopException>(() => _stock.AddStockAsync("NOPE", 1, "PO-1"));
        await Assert.ThrowsAsync<ShopException>(() => _stock.AddStockAsync("P1", 0, "PO-1"));

        Assert.Empty(_repo.Movements);
    }

    [Fact]
    public async Task Receive_adds_every_line_with_one_movement_each_under_the_same_reference()
    {
        var products = await _stock.ReceiveAsync("PO-7", [new("P1", 10), new("P2", 4)]);

        Assert.Equal([15, 7], products.Select(p => p.Quantity));
        Assert.Equal(15, _repo.OnHand("P1"));
        Assert.Equal(7, _repo.OnHand("P2"));
        Assert.Equal(2, _repo.Movements.Count);
        Assert.All(_repo.Movements, m => Assert.Equal("PO-7", m.Reference));
    }

    [Fact]
    public async Task Receive_changes_nothing_when_any_line_fails()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(
            () => _stock.ReceiveAsync("PO-8", [new("P1", 10), new("NOPE", 1)]));

        Assert.Equal(ShopError.NotFound, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
        Assert.Empty(_repo.Movements);
    }

    [Fact]
    public async Task Receive_with_no_lines_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(() => _stock.ReceiveAsync("PO-9", []));

        Assert.Equal(ShopError.Invalid, ex.Error);
    }

    [Fact]
    public async Task Receive_with_an_empty_entry_in_the_lines_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(
            () => _stock.ReceiveAsync("PO-11", [new("P1", 1), null!]));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task Receive_finds_products_whatever_the_case_of_the_code()
    {
        var products = await _stock.ReceiveAsync("PO-12", [new("p1", 10), new("P2", 4)]);

        Assert.Equal(["P1", "P2"], products.Select(p => p.ProductCode));
        Assert.Equal(15, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task Receive_with_the_same_product_in_two_cases_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(
            () => _stock.ReceiveAsync("PO-13", [new("P1", 1), new("p1", 2)]));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
    }

    [Fact]
    public async Task Receive_locks_products_in_code_order_whatever_order_the_lines_come_in()
    {
        await _stock.ReceiveAsync("PO-14", [new("P2", 1), new("P1", 1)]);

        Assert.Equal(["P1", "P2"], _repo.LockedCodes);
    }

    [Fact]
    public async Task Receive_with_the_same_product_twice_is_invalid()
    {
        var ex = await Assert.ThrowsAsync<ShopException>(
            () => _stock.ReceiveAsync("PO-10", [new("P1", 1), new("P1", 2)]));

        Assert.Equal(ShopError.Invalid, ex.Error);
        Assert.Equal(5, _repo.OnHand("P1"));
    }
}

namespace ShopApi.Models;

public class CartLine
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    /// <summary>Quantity of this product still free to add to a cart (so the cart can disable "+" without loading the product list).</summary>
    public int Available { get; set; }
}

public record Cart(IReadOnlyList<CartLine> Items)
{
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
}

public record Receipt(long OrderId, IReadOnlyList<CartLine> Items, decimal Total);

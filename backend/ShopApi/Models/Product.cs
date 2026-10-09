namespace ShopApi.Models;

public class Product
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity physically in stock.</summary>
    public int Quantity { get; set; }

    /// <summary>Quantity still free to add to a cart (in stock minus held by all carts).</summary>
    public int Available { get; set; }
}

/// <summary>One page of products. <paramref name="Next"/> is the code to pass as "after" for the next page, or null at the end.</summary>
public record ProductPage(IReadOnlyList<Product> Items, string? Next);

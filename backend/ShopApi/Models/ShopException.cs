namespace ShopApi.Models;

public enum ShopError
{
    /// <summary>Request is invalid (bad quantity, empty cart).</summary>
    Invalid,
    /// <summary>Product does not exist.</summary>
    NotFound,
    /// <summary>Not enough stock for the request.</summary>
    OutOfStock,
}

/// <summary>A business-rule failure. The exception handler turns it into an HTTP error.</summary>
public class ShopException(ShopError error, string message) : Exception(message)
{
    public ShopError Error { get; } = error;
}

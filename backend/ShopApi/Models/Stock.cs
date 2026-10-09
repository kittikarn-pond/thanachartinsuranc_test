namespace ShopApi.Models;

/// <summary>One line of a stock receipt: how many units of a product came in.</summary>
public record StockLine(string ProductCode, int Quantity);

/// <summary>A record of stock coming in. <c>Reference</c> is a supplier, PO number or note.</summary>
public record StockMovement(string ProductCode, int Quantity, string? Reference);

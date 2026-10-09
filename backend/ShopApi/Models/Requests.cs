namespace ShopApi.Models;

public record AddItemRequest(string ProductCode, int Quantity = 1);

public record SetQuantityRequest(int Quantity);

public record AddStockRequest(int Quantity, string? Reference = null);

public record ReceiveStockRequest(string? Reference, List<StockLine>? Lines);

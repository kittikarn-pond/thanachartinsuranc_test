namespace ShopApi.Data;

// One class per table. Only what the app reads or writes is mapped (see ShopDbContext for the column names).

public class ProductRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

public class StockRow
{
    public string Code { get; set; } = "";
    public int Quantity { get; set; }
}

public class StockMovementRow
{
    public long Id { get; set; }
    public string ProductCode { get; set; } = "";
    public int Quantity { get; set; }
    public string? Reference { get; set; }
}

public class CartRow
{
    public string Id { get; set; } = "";
}

public class CartItemRow
{
    public string CartId { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int Quantity { get; set; }
}

public class OrderRow
{
    public long Id { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemRow> Items { get; set; } = [];
}

public class OrderItemRow
{
    public long OrderId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

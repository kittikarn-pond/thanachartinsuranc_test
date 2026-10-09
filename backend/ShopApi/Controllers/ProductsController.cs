using Microsoft.AspNetCore.Mvc;
using ShopApi.Models;
using ShopApi.Services;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(ProductService products, StockService stock) : ControllerBase
{
    /// <summary>A page of products: ?search=&amp;after=&lt;Next of the previous page&gt;&amp;limit=50 (max 100).</summary>
    [HttpGet]
    public async Task<ActionResult<ProductPage>> GetPage(
        [FromQuery] string? search, [FromQuery] string? after, [FromQuery] int? limit) =>
        Ok(await products.GetPageAsync(search, after, limit));

    /// <summary>Specific products: ?codes=P001,P002 (max 200).</summary>
    [HttpGet("by-codes")]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetByCodes([FromQuery] string? codes) =>
        Ok(await products.GetByCodesAsync(
            (codes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

    [HttpPost("{productCode}/stock")]
    public async Task<ActionResult<Product>> AddStock(string productCode, AddStockRequest request) =>
        Ok(await stock.AddStockAsync(productCode, request.Quantity, request.Reference));
}

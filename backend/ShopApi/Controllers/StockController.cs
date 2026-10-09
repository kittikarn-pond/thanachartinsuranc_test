using Microsoft.AspNetCore.Mvc;
using ShopApi.Models;
using ShopApi.Services;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/stock")]
public class StockController(StockService stock) : ControllerBase
{
    [HttpPost("receipts")]
    public async Task<ActionResult<IReadOnlyList<Product>>> Receive(ReceiveStockRequest request) =>
        Ok(await stock.ReceiveAsync(request.Reference, request.Lines));
}

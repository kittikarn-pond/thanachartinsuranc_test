using Microsoft.AspNetCore.Mvc;
using ShopApi.Models;
using ShopApi.Services;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/carts/{cartId:guid}")]
public class CartsController(CartService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Cart>> Get(Guid cartId) =>
        Ok(await service.GetCartAsync(cartId));

    [HttpPost("items")]
    public async Task<ActionResult<Cart>> AddItem(Guid cartId, AddItemRequest request) =>
        Ok(await service.AddItemAsync(cartId, request.ProductCode, request.Quantity));

    [HttpPut("items/{productCode}")]
    public async Task<ActionResult<Cart>> SetQuantity(Guid cartId, string productCode, SetQuantityRequest request) =>
        Ok(await service.SetQuantityAsync(cartId, productCode, request.Quantity));

    [HttpDelete("items/{productCode}")]
    public async Task<ActionResult<Cart>> RemoveItem(Guid cartId, string productCode) =>
        Ok(await service.RemoveItemAsync(cartId, productCode));

    [HttpDelete]
    public async Task<ActionResult<Cart>> Clear(Guid cartId) =>
        Ok(await service.ClearAsync(cartId));

    [HttpPost("checkout")]
    public async Task<ActionResult<Receipt>> Checkout(Guid cartId) =>
        Ok(await service.CheckoutAsync(cartId));
}

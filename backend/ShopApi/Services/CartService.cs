using ShopApi.Data;
using ShopApi.Models;

namespace ShopApi.Services;

/// <summary>
/// Cart rules. Available stock = on-hand stock minus what carts hold.
/// Cart changes only move the held number; on-hand stock is deducted at checkout.
/// </summary>
public class CartService(ICartRepository repo)
{
    public Task<Cart> GetCartAsync(Guid cartId) => repo.GetCartAsync(cartId);

    public async Task<Cart> AddItemAsync(Guid cartId, string productCode, int quantity)
    {
        if (quantity <= 0)
            throw new ShopException(ShopError.Invalid, "Quantity must be greater than 0");

        return await repo.InTransactionAsync(async () =>
        {
            var (inCart, available) = await LockAndMeasureAsync(cartId, productCode);
            if (quantity > available)
                throw new ShopException(ShopError.OutOfStock, $"Not enough stock. Only {available} more available.");

            await repo.SetCartQuantityAsync(cartId, productCode, inCart + quantity);
            return await repo.GetCartAsync(cartId);
        });
    }

    /// <summary>Sets the exact quantity (increase or decrease). 0 removes the line.</summary>
    public async Task<Cart> SetQuantityAsync(Guid cartId, string productCode, int quantity)
    {
        if (quantity < 0)
            throw new ShopException(ShopError.Invalid, "Quantity must not be negative");

        return await repo.InTransactionAsync(async () =>
        {
            var (inCart, available) = await LockAndMeasureAsync(cartId, productCode);
            if (quantity - inCart > available)
                throw new ShopException(ShopError.OutOfStock, $"Not enough stock. Only {inCart + available} can be in cart.");

            await repo.SetCartQuantityAsync(cartId, productCode, quantity);
            return await repo.GetCartAsync(cartId);
        });
    }

    public async Task<Cart> RemoveItemAsync(Guid cartId, string productCode)
    {
        await repo.InTransactionAsync(() => repo.SetCartQuantityAsync(cartId, productCode, 0));
        return await repo.GetCartAsync(cartId);
    }

    public async Task<Cart> ClearAsync(Guid cartId)
    {
        await repo.InTransactionAsync(() => repo.ClearCartAsync(cartId));
        return await repo.GetCartAsync(cartId);
    }

    public Task<Receipt> CheckoutAsync(Guid cartId) =>
        repo.InTransactionAsync(async () =>
        {
            var cart = await repo.GetCartAsync(cartId);
            if (cart.Items.Count == 0)
                throw new ShopException(ShopError.Invalid, "Cart is empty");

            // Check every line before changing anything.
            foreach (var line in cart.Items)
            {
                var onHand = await repo.LockStockAsync(line.ProductCode) ?? 0;
                if (onHand < line.Quantity)
                    throw new ShopException(ShopError.OutOfStock, $"Not enough stock for {line.ProductCode}");
            }

            foreach (var line in cart.Items)
                await repo.DeductStockAsync(line.ProductCode, line.Quantity);

            var orderId = await repo.CreateOrderAsync(cart.Items, cart.Total);
            await repo.ClearCartAsync(cartId);
            return new Receipt(orderId, cart.Items, cart.Total);
        });

    // The lock on the stock row stops concurrent requests from overselling.
    private async Task<(int InCart, int Available)> LockAndMeasureAsync(Guid cartId, string productCode)
    {
        var onHand = await repo.LockStockAsync(productCode)
                     ?? throw new ShopException(ShopError.NotFound, "Product not found");
        var heldByAll = await repo.GetReservedAsync(productCode);
        var inCart = await repo.GetCartQuantityAsync(cartId, productCode);
        return (inCart, onHand - heldByAll);
    }
}

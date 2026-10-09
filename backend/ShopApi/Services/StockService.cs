using ShopApi.Data;
using ShopApi.Models;

namespace ShopApi.Services;

/// <summary>Stock coming in. Every addition is recorded in the movement history.</summary>
public class StockService(ICartRepository repo)
{
    private const int MaxReferenceLength = 200;

    public async Task<Product> AddStockAsync(string productCode, int quantity, string? reference = null)
    {
        reference = CleanReference(reference);
        await repo.InTransactionAsync(() => ReceiveLineAsync(productCode, quantity, reference));
        return (await repo.GetProductsByCodesAsync([productCode])).Single();
    }

    /// <summary>Receives a delivery: every line is added, or none are. Returns the products in line order.</summary>
    public async Task<IReadOnlyList<Product>> ReceiveAsync(string? reference, IReadOnlyList<StockLine>? lines)
    {
        reference = CleanReference(reference);
        if (lines is null || lines.Count == 0)
            throw new ShopException(ShopError.Invalid, "Receipt has no lines");
        if (lines.Any(l => l is null))
            throw new ShopException(ShopError.Invalid, "Receipt lines must not be empty");
        // The database compares codes case-insensitively, so "p001" and "P001" are the same product.
        if (lines.Select(l => l.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != lines.Count)
            throw new ShopException(ShopError.Invalid, "Each product can appear only once in a receipt");

        await repo.InTransactionAsync(async () =>
        {
            // Always lock in code order (checkout does too), so two deliveries cannot wait on each other.
            foreach (var line in lines.OrderBy(l => l.ProductCode, StringComparer.OrdinalIgnoreCase))
                await ReceiveLineAsync(line.ProductCode, line.Quantity, reference);
        });

        var products = await repo.GetProductsByCodesAsync(lines.Select(l => l.ProductCode).ToList());
        return lines.Select(l => products.Single(p => string.Equals(p.ProductCode, l.ProductCode, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    // The stock row lock doubles as the existence check: no row means no such product.
    private async Task ReceiveLineAsync(string productCode, int quantity, string? reference)
    {
        if (quantity <= 0)
            throw new ShopException(ShopError.Invalid, "Quantity must be greater than 0");

        var onHand = await repo.LockStockAsync(productCode)
                     ?? throw new ShopException(ShopError.NotFound, "Product not found");
        if ((long)onHand + quantity > int.MaxValue)
            throw new ShopException(ShopError.Invalid, "Stock would exceed the maximum");

        await repo.AddStockAsync(productCode, quantity);
        await repo.RecordMovementAsync(productCode, quantity, reference);
    }

    private static string? CleanReference(string? reference)
    {
        reference = reference?.Trim();
        if (string.IsNullOrEmpty(reference)) return null;
        if (reference.Length > MaxReferenceLength)
            throw new ShopException(ShopError.Invalid, $"Reference must be {MaxReferenceLength} characters or fewer");
        return reference;
    }
}

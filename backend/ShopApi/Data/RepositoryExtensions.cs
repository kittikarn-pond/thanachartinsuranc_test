namespace ShopApi.Data;

public static class RepositoryExtensions
{
    /// <summary>Transaction overload for work that returns nothing.</summary>
    public static Task InTransactionAsync(this ICartRepository repo, Func<Task> work) =>
        repo.InTransactionAsync(async () =>
        {
            await work();
            return true;
        });
}

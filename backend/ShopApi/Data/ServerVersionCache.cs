using Microsoft.EntityFrameworkCore;

namespace ShopApi.Data;

/// <summary>
/// Works out once whether the server is MySQL or MariaDB (and which version), then remembers it.
/// If the database is down the lookup fails, and the next request tries again.
/// </summary>
public static class ServerVersionCache
{
    private static ServerVersion? _version;

    public static ServerVersion For(string connectionString) =>
        _version ??= ServerVersion.AutoDetect(connectionString);
}

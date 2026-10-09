using Microsoft.AspNetCore.Diagnostics;
using ShopApi.Models;

namespace ShopApi.Controllers;

/// <summary>
/// Turns exceptions into a { message } body and logs them: business errors as warnings,
/// anything else as errors (the real cause stays in the log, not in the response).
/// </summary>
public class ShopExceptionHandler(ILogger<ShopExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, message) = exception is ShopException e
            ? (StatusFor(e.Error), e.Message)
            : (StatusCodes.Status500InternalServerError, "Something went wrong. Please try again.");

        if (exception is ShopException)
            log.LogWarning("{Method} {Path} rejected ({Status}): {Message}",
                context.Request.Method, context.Request.Path, status, message);
        else
            log.LogError(exception, "{Method} {Path} failed ({Status})",
                context.Request.Method, context.Request.Path, status);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message }, ct);
        return true;
    }

    private static int StatusFor(ShopError error) => error switch
    {
        ShopError.NotFound => StatusCodes.Status404NotFound,
        ShopError.OutOfStock => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}

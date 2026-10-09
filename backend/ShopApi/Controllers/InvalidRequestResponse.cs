using Microsoft.AspNetCore.Mvc;

namespace ShopApi.Controllers;

/// <summary>
/// The 400 for a request body ASP.NET rejects before it reaches a controller (missing field, wrong type,
/// bad JSON). Same { message } shape as every other error; the details go to the log.
/// </summary>
public static class InvalidRequestResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToList());

        var request = context.HttpContext.Request;
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(InvalidRequestResponse))
            .LogWarning("{Method} {Path} rejected (400): {Errors}",
                request.Method, request.Path,
                string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}")));

        return new BadRequestObjectResult(new { message = MessageFor(errors.Keys) });
    }

    private static string MessageFor(IEnumerable<string> keys)
    {
        // "$.quantity" and "ProductCode" name a field; "", "$" and "request" mean the body as a whole.
        var fields = keys
            .Select(key => key.StartsWith("$.") ? key[2..] : key)
            .Where(key => key is not ("" or "$" or "request"))
            .Select(key => char.ToLowerInvariant(key[0]) + key[1..])
            .Distinct()
            .ToList();

        return fields.Count > 0
            ? $"Invalid request: {string.Join(", ", fields)}"
            : "Invalid request: the body is missing or is not valid JSON";
    }
}

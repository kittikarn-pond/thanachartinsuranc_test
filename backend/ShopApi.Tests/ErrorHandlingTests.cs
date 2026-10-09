using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopApi.Controllers;
using ShopApi.Models;

namespace ShopApi.Tests;

public class ErrorHandlingTests
{
    private readonly LogCapture _log = new();

    private static DefaultHttpContext NewContext(IServiceProvider? services = null)
    {
        var http = new DefaultHttpContext();
        if (services is not null) http.RequestServices = services;
        http.Request.Method = "POST";
        http.Request.Path = "/api/carts/abc/items";
        http.Response.Body = new MemoryStream();
        return http;
    }

    private static string? MessageIn(HttpContext http)
    {
        http.Response.Body.Position = 0;
        return JsonDocument.Parse(http.Response.Body).RootElement.GetProperty("message").GetString();
    }

    private async Task<DefaultHttpContext> HandleAsync(Exception exception)
    {
        var http = NewContext();
        var handler = new ShopExceptionHandler(_log.Factory.CreateLogger<ShopExceptionHandler>());
        Assert.True(await handler.TryHandleAsync(http, exception, CancellationToken.None));
        return http;
    }

    [Theory]
    [InlineData(ShopError.Invalid, 400)]
    [InlineData(ShopError.NotFound, 404)]
    [InlineData(ShopError.OutOfStock, 409)]
    public async Task Business_error_becomes_its_status_with_the_message(ShopError error, int status)
    {
        var http = await HandleAsync(new ShopException(error, "Not enough stock"));

        Assert.Equal(status, http.Response.StatusCode);
        Assert.Equal("Not enough stock", MessageIn(http));
    }

    [Fact]
    public async Task Business_error_is_logged_as_a_warning_with_the_request_and_message()
    {
        await HandleAsync(new ShopException(ShopError.OutOfStock, "Not enough stock"));

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("POST /api/carts/abc/items", entry.Message);
        Assert.Contains("409", entry.Message);
        Assert.Contains("Not enough stock", entry.Message);
    }

    [Fact]
    public async Task Unexpected_error_is_logged_with_its_exception_and_hides_the_details()
    {
        var boom = new InvalidOperationException("connection string is secret");

        var http = await HandleAsync(boom);

        Assert.Equal(500, http.Response.StatusCode);
        Assert.DoesNotContain("secret", MessageIn(http));
        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(boom, entry.Exception);
    }

    [Fact]
    public void Bad_request_body_becomes_a_message_naming_the_field_and_is_logged()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("ProductCode", "The ProductCode field is required.");
        state.AddModelError("$.quantity", "The JSON value could not be converted to System.Int32.");

        var result = BuildInvalidRequestResponse(state);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Invalid request: productCode, quantity", MessageOf(result));
        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("POST /api/carts/abc/items", entry.Message);
        Assert.Contains("The ProductCode field is required.", entry.Message);
    }

    [Fact]
    public void Empty_or_unreadable_body_says_the_body_is_the_problem()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("", "A non-empty request body is required.");
        state.AddModelError("request", "The request field is required.");

        var result = BuildInvalidRequestResponse(state);

        Assert.Equal("Invalid request: the body is missing or is not valid JSON", MessageOf(result));
    }

    private ObjectResult BuildInvalidRequestResponse(ModelStateDictionary state)
    {
        var services = new ServiceCollection().AddSingleton(_log.Factory).BuildServiceProvider();
        var http = NewContext(services);
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor(), state);
        return (ObjectResult)InvalidRequestResponse.Create(context);
    }

    private static string? MessageOf(ObjectResult result) =>
        JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString();
}

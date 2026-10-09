using Serilog;
using ShopApi.Controllers;
using ShopApi.Data;
using ShopApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Console plus a daily file in logs/ (see the "Serilog" section of appsettings.json).
builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = InvalidRequestResponse.Create);
builder.Services.AddScoped<ICartRepository, MySqlCartRepository>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddExceptionHandler<ShopExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.MapControllers();

app.Run();

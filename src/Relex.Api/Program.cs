using Microsoft.EntityFrameworkCore;
using Relex.Api.Features.Orders;
using Relex.Api.Features.Locations;
using Relex.Api.Features.Products;
using Relex.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "Relex API", 
        Version = "v1",
        Description = "High-performance API for Replenishment Orders"
    });

    // XML Comments Integration
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Database Context Registration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<RelexDbContext>(options =>
    options.UseNpgsql(connectionString));

// Optimization: Singleton Cache
builder.Services.AddSingleton<LookupCache>();
builder.Services.AddSingleton<ILookupCache>(sp => sp.GetRequiredService<LookupCache>());

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RelexDbContext>();

// Global Exception Handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure Middleware Pipeline
app.UseExceptionHandler();

// Warm up cache
using (var scope = app.Services.CreateScope())
{
    var cache = scope.ServiceProvider.GetRequiredService<LookupCache>();
    // Waiting ensures consistency for first request.
    await cache.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Feature Endpoints
app.MapGetOrder();
app.MapCreateOrder();
app.MapListOrders();
app.MapGetOrderStats();
app.MapBulkUpsertOrders();
app.MapUpdateOrder();
app.MapDeleteOrder();
app.MapListLocations();
app.MapListProducts();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }

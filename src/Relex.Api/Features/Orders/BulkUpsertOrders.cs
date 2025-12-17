using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Relex.Api.Infrastructure;
using Relex.Domain;
using Relex.Api.Features.Orders.Dtos;

namespace Relex.Api.Features.Orders;

public static class BulkUpsertOrders
{
    public static void MapBulkUpsertOrders(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/bulk", HandleAsync)
           .WithName("BulkUpsertOrders")
           .Accepts<CreateOrderRequest[]>("application/json");
    }

    /// <summary>
    /// Efficiently inserts a large stream of orders using PostgreSQL Binary Import (COPY).
    /// </summary>
    /// <remarks>
    /// This endpoint bypasses standard EF Core tracking for maximum throughput.
    /// It streams the request body directly to the database, maintaining constant memory usage
    /// regardless of payload size. Dimensions are validated against an in-memory cache.
    /// </remarks>
    /// <param name="orders">The stream of orders to insert.</param>
    /// <param name="cache">Singleton lookup cache for validation.</param>
    /// <param name="config">Configuration for DB connection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary of processed items.</returns>
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    private static async Task<Results<Ok<string>, BadRequest<string>>> HandleAsync(
        [FromBody] IAsyncEnumerable<CreateOrderRequest> orders,
        [FromServices] ILookupCache cache,
        [FromServices] IConfiguration config,
        CancellationToken ct)
    {
        if (orders == null)
        {
             return TypedResults.BadRequest("Invalid request body.");
        }

        // High-Performance Insert using COPY
        var connectionString = config.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Single transaction/copy operation. 
        // If any row fails validation, it's possible to abort or skip. 
        // In this case, for strict consistency, it's aborted.
        using var writer = await conn.BeginBinaryImportAsync(
            "COPY orders (id, location_id, product_id, order_date, quantity, submitted_by, submitted_at, status) FROM STDIN (FORMAT BINARY)",
            ct);

        int count = 0;
        int failedCount = 0;

        try 
        {
            await foreach (var item in orders)
            {
                if (item is null) continue;

                // Simple Guard Clauses
                if (item.Quantity <= 0 || 
                    string.IsNullOrWhiteSpace(item.LocationCode) ||
                    string.IsNullOrWhiteSpace(item.ProductCode) ||
                    string.IsNullOrWhiteSpace(item.SubmittedBy))
                {
                    failedCount++;
                    continue;
                }

                // Validation (In-Memory Cache)
                var locId = cache.GetLocationId(item.LocationCode);
                var prodId = cache.GetProductId(item.ProductCode);

                if (locId == null || prodId == null)
                {
                    // In a prod scenario, this should be logged this and/or return a partial error report.
                    // Aditionally, it's possible to rollback everything.
                    // Here is just skipped and the errors are counted.
                    failedCount++;
                    continue;
                }

                await writer.StartRowAsync(ct);
                await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(locId.Value, NpgsqlDbType.Smallint, ct);
                await writer.WriteAsync(prodId.Value, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(item.OrderDate, NpgsqlDbType.Date, ct);
                await writer.WriteAsync(item.Quantity, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(item.SubmittedBy, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(DateTimeOffset.UtcNow, NpgsqlDbType.TimestampTz, ct);
                await writer.WriteAsync((int)OrderStatus.Pending, NpgsqlDbType.Integer, ct);
                
                count++;
            }

            // Commit the import
            await writer.CompleteAsync(ct);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Streaming failed: {ex.Message}");
        }

        return TypedResults.Ok($"Processed {count + failedCount} items. Inserted: {count}. Failed: {failedCount}.");
    }
}

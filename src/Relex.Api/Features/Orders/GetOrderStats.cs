using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Infrastructure;
using Relex.Api.Features.Orders.Dtos;

namespace Relex.Api.Features.Orders;

public static class GetOrderStats
{
    public static void MapGetOrderStats(this IEndpointRouteBuilder app)
    {
        // Adapting "GET /orders?aggregate=true" to a distinct endpoint for cleaner typing
        app.MapGet("/orders/stats", HandleAsync)
           .WithName("GetOrderStats");
    }

    [ProducesResponseType(typeof(OrderStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    private static async Task<Results<Ok<OrderStatsResponse>, BadRequest<string>>> HandleAsync(
        RelexDbContext db,
        CancellationToken ct,
        [FromQuery] string? locationCode = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return TypedResults.BadRequest("StartDate cannot be after EndDate.");
        }

        var query = db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(locationCode))
        {
            query = query.Where(o => o.Location!.Code == locationCode);
        }

        if (startDate.HasValue)
        {
            query = query.Where(o => o.OrderDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(o => o.OrderDate <= endDate.Value);
        }

        // Calculate aggregates
        var stats = await query
            .GroupBy(x => 1) // Global aggregation
            .Select(g => new OrderStatsResponse
            {
                TotalOrders = g.Count(),
                TotalQuantity = g.Sum(o => (long)o.Quantity),
                AverageQuantity = g.Average(o => o.Quantity),
                FirstOrderDate = g.Min(o => (DateOnly?)o.OrderDate),
                LastOrderDate = g.Max(o => (DateOnly?)o.OrderDate)
            })
            .FirstOrDefaultAsync(ct);

        return TypedResults.Ok(stats ?? new OrderStatsResponse 
        { 
            TotalOrders = 0, 
            TotalQuantity = 0, 
            AverageQuantity = 0, 
            FirstOrderDate = null, 
            LastOrderDate = null 
        });
    }
}

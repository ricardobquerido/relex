using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Infrastructure;
using Relex.Domain;
using Relex.Api.Features.Orders.Dtos;

namespace Relex.Api.Features.Orders;

public static class ListOrders
{
    public static void MapListOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders", HandleAsync)
           .WithName("ListOrders");
    }

    /// <summary>
    /// Retrieves a paged list of orders.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="locationCode">Optional. Filter by Location Code.</param>
    /// <param name="startDate">Optional. Filter orders on or after this date.</param>
    /// <param name="endDate">Optional. Filter orders on or before this date.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <returns>Paged result of orders.</returns>
    [ProducesResponseType(typeof(ListOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    private static async Task<Results<Ok<ListOrdersResponse>, BadRequest<string>>> HandleAsync(
        RelexDbContext db,
        CancellationToken ct,
        [FromQuery] string? locationCode = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Guard Clauses
        if (page < 1)
        {
            return TypedResults.BadRequest("Page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return TypedResults.BadRequest("PageSize must be between 1 and 100.");
        }

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return TypedResults.BadRequest("StartDate cannot be after EndDate.");
        }

        // Base Query
        var query = db.Orders.AsNoTracking();

        // 1. Filtering
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

        // 2. Counting (might be slow on 100M rows without specific optimization, but standard for REST APIs)
        var totalCount = await query.CountAsync(ct);

        // 3. Paging & Materialization
        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Location)
            .Include(o => o.Product)
            .ToListAsync(ct);

        var dtos = items.Select(o => o.ToDto()).ToList();

        return TypedResults.Ok(new ListOrdersResponse 
        { 
            Data = dtos, 
            Page = page, 
            PageSize = pageSize, 
            TotalCount = totalCount 
        });
    }
}

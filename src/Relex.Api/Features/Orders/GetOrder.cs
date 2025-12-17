using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Infrastructure;
using Relex.Domain;
using Relex.Api.Features.Orders.Dtos;

namespace Relex.Api.Features.Orders;

public static class GetOrder
{
    public static void MapGetOrder(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/{id:guid}", HandleAsync)
           .WithName("GetOrder");
    }

    /// <summary>
    /// Retrieves a specific order by ID.
    /// </summary>
    /// <remarks>
    /// Returns the order details including resolved location and product codes.
    /// </remarks>
    /// <param name="id">The unique identifier of the order.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The requested order DTO.</returns>
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public static async Task<Results<Ok<OrderDto>, NotFound>> HandleAsync(
        [FromRoute] Guid id,
        RelexDbContext db,
        CancellationToken ct)
    {
        // Using AsNoTracking for read performance
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Location)
            .Include(o => o.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(order.ToDto());
    }
}

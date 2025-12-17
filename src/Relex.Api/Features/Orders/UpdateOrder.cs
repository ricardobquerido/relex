using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Infrastructure;
using Relex.Domain;
using Relex.Api.Features.Orders.Dtos;

namespace Relex.Api.Features.Orders;

public static class UpdateOrder
{
    public static void MapUpdateOrder(this IEndpointRouteBuilder app)
    {
        app.MapPut("/orders/{id:guid}", HandleAsync)
           .WithName("UpdateOrder");
    }

    /// <summary>
    /// Updates an existing order.
    /// </summary>
    /// <remarks>
    /// Allows modifying quantity and status. Updates the SubmittedAt timestamp.
    /// </remarks>
    /// <param name="id">The unique identifier of the order to update.</param>
    /// <param name="request">The update details.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public static async Task<Results<NoContent, NotFound, BadRequest<string>>> HandleAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateOrderRequest request,
        RelexDbContext db,
        CancellationToken ct)
    {
        // Guard Clause
        if (request.Quantity <= 0)
        {
            return TypedResults.BadRequest("Quantity must be greater than zero.");
        }

        // Validate Status if provided
        if (!string.IsNullOrWhiteSpace(request.Status) && !Enum.TryParse<OrderStatus>(request.Status, true, out _))
        {
            return TypedResults.BadRequest($"Invalid status: {request.Status}. Valid values are: {string.Join(", ", Enum.GetNames<OrderStatus>())}");
        }

        // Optimized Lookup: Use Partition Key (OrderDate) if provided to prune partitions
        var query = db.Orders.AsQueryable();

        if (request.OrderDate.HasValue)
        {
            query = query.Where(o => o.OrderDate == request.OrderDate.Value);
        }

        var order = await query.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
        {
            return TypedResults.NotFound();
        }

        order.Quantity = request.Quantity;
        
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            order.Status = Enum.Parse<OrderStatus>(request.Status, true);
        }

        order.SubmittedAt = DateTimeOffset.UtcNow; // Update timestamp

        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }
}

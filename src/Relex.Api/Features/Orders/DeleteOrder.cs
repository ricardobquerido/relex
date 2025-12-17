using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Infrastructure;
using Relex.Domain;

namespace Relex.Api.Features.Orders;

public static class DeleteOrder
{
    public static void MapDeleteOrder(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/orders/{id:guid}", HandleAsync)
           .WithName("DeleteOrder");
    }

    /// <summary>
    /// Deletes an existing order.
    /// </summary>
    /// <remarks>
    /// Only "Pending" orders can be deleted. Past orders cannot be deleted.
    /// </remarks>
    /// <param name="id">The unique identifier of the order to delete.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public static async Task<Results<NoContent, NotFound, BadRequest<string>>> HandleAsync(
        [FromRoute] Guid id,
        RelexDbContext db,
        CancellationToken ct)
    {
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
        {
            return TypedResults.NotFound();
        }

        // Validation: Prevent deleting past orders
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (order.OrderDate < today)
        {
            return TypedResults.BadRequest("Cannot delete orders from the past.");
        }

        // Validation: Only Pending orders can be deleted
        if (order.Status != OrderStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot delete order with status '{order.Status}'.");
        }

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }
}

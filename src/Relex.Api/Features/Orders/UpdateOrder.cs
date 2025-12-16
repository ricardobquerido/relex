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

        // TODO: need the partition key (OrderDate) in the request URL or body to avoid scanning all partitions 
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == id, ct);

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

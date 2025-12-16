using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Domain;
using Relex.Api.Infrastructure;
using Relex.Api.Features.Orders.Dtos;

namespace Relex.Api.Features.Orders;

public static class CreateOrder
{
    public static void MapCreateOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", HandleAsync)
           .WithName("CreateOrder");
    }

    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public static async Task<Results<Created<OrderDto>, BadRequest<string>>> HandleAsync(
        [FromBody] CreateOrderRequest request,
        RelexDbContext db,
        ILookupCache cache,
        CancellationToken ct)
    {
        // Simple Guard Clauses (Built-in Validation)
        if (request.Quantity <= 0)
        {
            return TypedResults.BadRequest("Quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.LocationCode))
        {
            return TypedResults.BadRequest("LocationCode is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProductCode))
        {
            return TypedResults.BadRequest("ProductCode is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SubmittedBy))
        {
            return TypedResults.BadRequest("SubmittedBy is required.");
        }

        // 1. Resolve Lookups (In-Memory)
        var locationId = cache.GetLocationId(request.LocationCode);
        if (locationId == null)
        {
            return TypedResults.BadRequest($"Invalid Location Code: {request.LocationCode}");
        }

        var productId = cache.GetProductId(request.ProductCode);
        if (productId == null)
        {
            return TypedResults.BadRequest($"Invalid Product Code: {request.ProductCode}");
        }

        // 2. Create Entity
        var order = new Order
        {
            Id = Guid.NewGuid(),
            LocationId = locationId.Value,
            ProductId = productId.Value,
            OrderDate = request.OrderDate,
            Quantity = request.Quantity,
            SubmittedBy = request.SubmittedBy,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // 3. Return DTO
        var responseDto = new OrderDto
        {
            Id = order.Id,
            LocationCode = request.LocationCode,
            ProductCode = request.ProductCode,
            OrderDate = order.OrderDate,
            Quantity = order.Quantity,
            SubmittedBy = order.SubmittedBy,
            SubmittedAt = order.SubmittedAt,
            Status = order.Status.ToString()
        };

        return TypedResults.Created($"/orders/{order.Id}", responseDto);
    }
}

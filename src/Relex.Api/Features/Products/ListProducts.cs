using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Features.Products.Dtos;
using Relex.Api.Infrastructure;
using Relex.Domain;

namespace Relex.Api.Features.Products;

public static class ListProducts
{
    public static void MapListProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", HandleAsync)
           .WithName("ListProducts");
    }

    /// <summary>
    /// Retrieves a list of all available products.
    /// </summary>
    /// <remarks>
    /// Returns a lightweight list of product codes and IDs.
    /// </remarks>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of products.</returns>
    [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
    private static async Task<Ok<List<ProductDto>>> HandleAsync(
        RelexDbContext db,
        CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new ProductDto { Id = p.Id, Code = p.Code })
            .ToListAsync(ct);

        return TypedResults.Ok(products);
    }
}

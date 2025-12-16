using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relex.Api.Features.Locations.Dtos;
using Relex.Api.Infrastructure;
using Relex.Domain;

namespace Relex.Api.Features.Locations;

public static class ListLocations
{
    public static void MapListLocations(this IEndpointRouteBuilder app)
    {
        app.MapGet("/locations", HandleAsync)
           .WithName("ListLocations");
    }

    [ProducesResponseType(typeof(List<LocationDto>), StatusCodes.Status200OK)]
    private static async Task<Ok<List<LocationDto>>> HandleAsync(
        RelexDbContext db,
        CancellationToken ct)
    {
        var locations = await db.Locations
            .AsNoTracking()
            .OrderBy(l => l.Code)
            .Select(l => new LocationDto { Id = l.Id, Code = l.Code })
            .ToListAsync(ct);

        return TypedResults.Ok(locations);
    }
}

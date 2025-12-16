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
           .WithName("BulkUpsertOrders");
    }
}

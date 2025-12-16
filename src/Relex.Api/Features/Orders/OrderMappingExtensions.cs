using Relex.Api.Features.Orders.Dtos;
using Relex.Domain;

namespace Relex.Api.Features.Orders;

public static class OrderMappingExtensions
{
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            LocationCode = order.Location?.Code ?? "UNKNOWN", // Should be loaded via Include
            ProductCode = order.Product?.Code ?? "UNKNOWN",
            OrderDate = order.OrderDate,
            Quantity = order.Quantity,
            SubmittedBy = order.SubmittedBy,
            SubmittedAt = order.SubmittedAt,
            Status = order.Status.ToString()
        };
    }
}

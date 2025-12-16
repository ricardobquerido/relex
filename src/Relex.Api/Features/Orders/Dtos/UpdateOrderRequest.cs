namespace Relex.Api.Features.Orders.Dtos;

/// <summary>
/// Request payload for updating an existing order.
/// </summary>
public record UpdateOrderRequest
{
    /// <summary>
    /// New quantity for the order. Must be greater than 0.
    /// </summary>
    /// <example>50</example>
    public int Quantity { get; init; }

    /// <summary>
    /// Optional: New status for the order (Pending, Confirmed, Shipped, Cancelled).
    /// </summary>
    /// <example>Confirmed</example>
    public string? Status { get; init; }
}

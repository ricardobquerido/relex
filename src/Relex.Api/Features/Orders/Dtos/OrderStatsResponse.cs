namespace Relex.Api.Features.Orders.Dtos;

/// <summary>
/// Aggregated statistics for orders.
/// </summary>
public record OrderStatsResponse
{
    /// <summary>
    /// Total count of orders matching criteria.
    /// </summary>
    /// <example>500</example>
    public int TotalOrders { get; init; }

    /// <summary>
    /// Sum of all quantities.
    /// </summary>
    /// <example>15000</example>
    public long TotalQuantity { get; init; }

    /// <summary>
    /// Average quantity per order.
    /// </summary>
    /// <example>30.5</example>
    public double AverageQuantity { get; init; }

    /// <summary>
    /// Date of the earliest order found.
    /// </summary>
    /// <example>2023-01-01</example>
    public DateOnly? FirstOrderDate { get; init; }

    /// <summary>
    /// Date of the latest order found.
    /// </summary>
    /// <example>2023-12-31</example>
    public DateOnly? LastOrderDate { get; init; }

    public OrderStatsResponse() { }

    public OrderStatsResponse(int totalOrders, long totalQuantity, double averageQuantity, DateOnly? firstOrderDate, DateOnly? lastOrderDate)
    {
        TotalOrders = totalOrders;
        TotalQuantity = totalQuantity;
        AverageQuantity = averageQuantity;
        FirstOrderDate = firstOrderDate;
        LastOrderDate = lastOrderDate;
    }
}

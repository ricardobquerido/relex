namespace Relex.Api.Features.Orders.Dtos;

/// <summary>
/// Request payload for creating a new order.
/// </summary>
public record CreateOrderRequest
{
    /// <summary>
    /// Unique code for the location.
    /// </summary>
    /// <example>LOC-0001</example>
    public required string LocationCode { get; init; }

    /// <summary>
    /// Unique code for the product.
    /// </summary>
    /// <example>PROD-00001</example>
    public required string ProductCode { get; init; }

    /// <summary>
    /// The date of the order.
    /// </summary>
    /// <example>2023-05-20</example>
    public DateOnly OrderDate { get; init; }

    /// <summary>
    /// Quantity ordered. Must be greater than 0.
    /// </summary>
    /// <example>100</example>
    public int Quantity { get; init; }

    /// <summary>
    /// Email or username of the submitter.
    /// </summary>
    /// <example>user@relex.com</example>
    public required string SubmittedBy { get; init; }
}

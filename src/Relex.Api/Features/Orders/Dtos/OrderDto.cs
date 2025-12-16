namespace Relex.Api.Features.Orders.Dtos;

/// <summary>
/// Represents a replenishment order.
/// </summary>
public record OrderDto
{
    /// <summary>
    /// The unique identifier of the order.
    /// </summary>
    /// <example>a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11</example>
    public Guid Id { get; init; }

    /// <summary>
    /// The location code (e.g. LOC-001).
    /// </summary>
    /// <example>LOC-0001</example>
    public required string LocationCode { get; init; }

    /// <summary>
    /// The product code (e.g. PROD-123).
    /// </summary>
    /// <example>PROD-00001</example>
    public required string ProductCode { get; init; }

    /// <summary>
    /// The date the order is for.
    /// </summary>
    /// <example>2023-05-20</example>
    public DateOnly OrderDate { get; init; }

    /// <summary>
    /// The quantity ordered.
    /// </summary>
    /// <example>100</example>
    public int Quantity { get; init; }

    /// <summary>
    /// User who submitted the order.
    /// </summary>
    /// <example>user@relex.com</example>
    public required string SubmittedBy { get; init; }

    /// <summary>
    /// Timestamp of submission (UTC).
    /// </summary>
    /// <example>2023-05-20T10:00:00Z</example>
    public DateTimeOffset SubmittedAt { get; init; }

    /// <summary>
    /// Current status of the order.
    /// </summary>
    /// <example>Confirmed</example>
    public required string Status { get; init; }

    public OrderDto() { }

    public OrderDto(Guid id, string locationCode, string productCode, DateOnly orderDate, int quantity, string submittedBy, DateTimeOffset submittedAt, string status)
    {
        Id = id;
        LocationCode = locationCode;
        ProductCode = productCode;
        OrderDate = orderDate;
        Quantity = quantity;
        SubmittedBy = submittedBy;
        SubmittedAt = submittedAt;
        Status = status;
    }
}

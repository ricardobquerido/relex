namespace Relex.Api.Features.Orders.Dtos;

/// <summary>
/// Paginated response for listing orders.
/// </summary>
public record ListOrdersResponse
{
    /// <summary>
    /// List of orders for the current page.
    /// </summary>
    public required List<OrderDto> Data { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    /// <example>1</example>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    /// <example>20</example>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    /// <example>1000</example>
    public int TotalCount { get; init; }

    public ListOrdersResponse() { }

    public ListOrdersResponse(List<OrderDto> data, int page, int pageSize, int totalCount)
    {
        Data = data;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}

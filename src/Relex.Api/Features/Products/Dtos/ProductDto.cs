namespace Relex.Api.Features.Products.Dtos;

/// <summary>
/// Represents a product in the system.
/// </summary>
public record ProductDto
{
    /// <summary>
    /// Internal ID of the product.
    /// </summary>
    /// <example>101</example>
    public int Id { get; init; }

    /// <summary>
    /// Unique code of the product.
    /// </summary>
    /// <example>PROD-00001</example>
    public required string Code { get; init; }
}

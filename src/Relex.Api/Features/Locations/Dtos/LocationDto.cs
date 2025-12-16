namespace Relex.Api.Features.Locations.Dtos;

/// <summary>
/// Represents a location in the system.
/// </summary>
public record LocationDto
{
    /// <summary>
    /// Internal ID of the location.
    /// </summary>
    /// <example>1</example>
    public short Id { get; init; }

    /// <summary>
    /// Unique code of the location.
    /// </summary>
    /// <example>LOC-0001</example>
    public required string Code { get; init; }
}

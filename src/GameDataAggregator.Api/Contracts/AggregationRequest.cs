using System.ComponentModel.DataAnnotations;
using GameDataAggregator.Api.Validation;
using GameDataAggregator.Core.Models;

namespace GameDataAggregator.Api.Contracts;

/// <summary>Query-string parameters of <c>GET /api/v1/aggregation</c>.</summary>
/// <remarks>
/// Every field has its own validation attribute, so all field errors are returned together in one 400 response.
/// Enums are bound by name only, and dates without an offset are read as UTC (see the ModelBinding folder).
/// </remarks>
public sealed class AggregationRequest : IValidatableObject
{
    /// <summary>Game name to search for in every source, e.g. "witcher". 2 to 100 characters, not counting outer spaces.</summary>
    [Required]
    [TrimmedStringLength(100, MinimumLength = 2)]
    public string? Search { get; init; }

    /// <summary>Comma-separated list of sources to query; <c>GET /api/v1/sources</c> lists the valid names. Default: all.</summary>
    [KnownSources]
    public string? Sources { get; init; }

    /// <summary>Comma-separated list of categories (GameInfo, Deal, PlayerActivity). Default: all.</summary>
    [EnumNameList<ItemCategory>]
    public string? Categories { get; init; }

    /// <summary>Only items with this tag, e.g. a genre ("RPG") or a store ("Steam").</summary>
    [StringLength(50)]
    public string? Tag { get; init; }

    /// <summary>Only items dated on or after this date (ISO 8601). Without an offset, the value is UTC.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only items dated on or before this date (ISO 8601). Without an offset, the value is UTC.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Sort field: Relevance (default), Date, Title or Source.</summary>
    public SortField SortBy { get; init; } = SortField.Relevance;

    /// <summary>Ascending or Descending (default).</summary>
    public SortDirection SortDirection { get; init; } = SortDirection.Descending;

    /// <summary>1-based page number.</summary>
    [Range(1, 1000)]
    public int Page { get; init; } = 1;

    /// <summary>Items per page (1-100).</summary>
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    /// <summary>Turns the checked request into the search the service runs.</summary>
    public AggregationQuery ToQuery() => new()
    {
        Search = Search!.Trim(),
        Sources = QueryList.Split(Sources).ToHashSet(StringComparer.OrdinalIgnoreCase),
        Categories = QueryList.Split(Categories).Select(name => Enum.Parse<ItemCategory>(name, ignoreCase: true)).ToHashSet(),
        Tag = string.IsNullOrWhiteSpace(Tag) ? null : Tag.Trim(),
        From = From,
        To = To,
        SortBy = SortBy,
        SortDirection = SortDirection,
        Page = Page,
        PageSize = PageSize,
    };

    /// <summary>
    /// Checks that compare fields. ASP.NET Core runs this only once every field is valid on its own,
    /// which is the right order: comparing two values is meaningless while one of them is invalid.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        //  Reject a start date that comes after the end date.
        if (From > To)
        {
            yield return new ValidationResult("'from' must be earlier than or equal to 'to'.", [nameof(From), nameof(To)]);
        }
    }
}

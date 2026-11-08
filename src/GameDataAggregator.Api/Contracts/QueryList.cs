namespace GameDataAggregator.Api.Contracts;

/// <summary>Parses comma-separated query-string lists such as <c>sources=RAWG,Steam</c>.</summary>
internal static class QueryList
{
    /// <summary>Splits the text at commas, trimming each entry and dropping empty ones.</summary>
    public static IReadOnlyList<string> Split(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

namespace GameDataAggregator.Core.Aggregation;

/// <summary>
/// Scores how well a title matches a search term, from 0 to 1. Every source is scored with the same
/// rule, which keeps relevance comparable across APIs that rank results differently (or not at all).
/// <para>
/// Match types, strongest first: exact, title starts with the term, title contains the term, some of
/// the term's words appear in the title. Within a match type, titles where the term covers more of
/// the title score higher, so "The Witcher 3" beats "The Witcher 3: Wild Hunt - Blood and Wine".
/// </para>
/// </summary>
public static class RelevanceScorer
{
    private const double ExactMatch = 1.0;
    private const double PrefixMatch = 0.8;
    private const double ContainsMatch = 0.6;
    private const double MaxCoverageBonus = 0.15;
    private const double MaxWordOverlap = 0.5;

    private static readonly char[] Separators = [' ', '-', '_', ':', ',', '.', '\'', '"', '!', '?', '(', ')', '/', '&', '—', '–'];

    /// <summary>Rates how closely a title matches the search, from 0 to 1.</summary>
    public static double Score(string search, string title)
    {
        //  Compare both texts in lower case, without punctuation.
        var normalizedSearch = Normalize(search);
        var normalizedTitle = Normalize(title);

        //  Score nothing when either text is empty.
        if (normalizedSearch.Length == 0 || normalizedTitle.Length == 0)
        {
            return 0;
        }

        //  Score an exact match highest.
        if (normalizedTitle == normalizedSearch)
        {
            return ExactMatch;
        }

        //  Measure how much of the title the search covers.
        var coverage = (double)normalizedSearch.Length / normalizedTitle.Length;

        //  Rank titles that start with the search next, the closer in length the higher.
        if (normalizedTitle.StartsWith(normalizedSearch, StringComparison.Ordinal))
        {
            return PrefixMatch + (MaxCoverageBonus * coverage);
        }

        //  Rank titles that contain the search after those.
        if (normalizedTitle.Contains(normalizedSearch, StringComparison.Ordinal))
        {
            return ContainsMatch + (MaxCoverageBonus * coverage);
        }

        //  Otherwise give partial credit for each search word found in the title.
        var searchWords = Tokenize(normalizedSearch);
        var titleWords = Tokenize(normalizedTitle).ToHashSet(StringComparer.Ordinal);
        var matched = searchWords.Count(titleWords.Contains);

        return MaxWordOverlap * matched / searchWords.Length;
    }

    /// <summary>Lower-cases and collapses punctuation/whitespace, so "The Witcher 3: Wild Hunt" becomes "the witcher 3 wild hunt".</summary>
    private static string Normalize(string value) =>
        string.Join(' ', Tokenize(value.ToLowerInvariant()));

    /// <summary>Splits text into words, dropping punctuation.</summary>
    private static string[] Tokenize(string value) =>
        value.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

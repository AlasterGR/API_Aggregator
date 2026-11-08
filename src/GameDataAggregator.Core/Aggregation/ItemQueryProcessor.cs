using GameDataAggregator.Core.Models;

namespace GameDataAggregator.Core.Aggregation;

/// <summary>
/// Applies the filter, sort and paging options of an <see cref="AggregationQuery"/> to a set of items.
/// Pure and side-effect free, so it is trivial to unit test.
/// </summary>
public static class ItemQueryProcessor
{
    /// <summary>Filters, sorts and pages the items, and counts how many matched.</summary>
    public static (IReadOnlyList<AggregatedItem> Page, int TotalCount) Apply(
        IEnumerable<AggregatedItem> items,
        AggregationQuery query)
    {
        //  Refuse to run without items or a query.
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);

        //  Keep the matching items, then sort them and cut out the requested page.
        var filtered = Filter(items, query).ToList();
        var page = Sort(filtered, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return (page, filtered.Count);
    }

    /// <summary>Keeps only the items that pass every filter in the query.</summary>
    private static IEnumerable<AggregatedItem> Filter(IEnumerable<AggregatedItem> items, AggregationQuery query)
    {
        //  Keep only the chosen sources.
        if (query.Sources.Count > 0)
        {
            items = items.Where(i => query.Sources.Contains(i.Source));
        }

        //  Keep only the chosen categories.
        if (query.Categories.Count > 0)
        {
            items = items.Where(i => query.Categories.Contains(i.Category));
        }

        //  Keep only items with the chosen tag.
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            items = items.Where(i => i.Tags.Contains(query.Tag, StringComparer.OrdinalIgnoreCase));
        }

        //  Drop items dated before the start date.
        if (query.From is { } from)
        {
            items = items.Where(i => i.Date >= from);
        }

        //  Drop items dated after the end date.
        if (query.To is { } to)
        {
            items = items.Where(i => i.Date <= to);
        }

        return items;
    }

    /// <summary>Orders the items by the chosen field and direction.</summary>
    private static IOrderedEnumerable<AggregatedItem> Sort(IEnumerable<AggregatedItem> items, AggregationQuery query)
    {
        //  Note whether the order runs from high to low.
        var descending = query.SortDirection == SortDirection.Descending;

        //  Order by the chosen field.
        var ordered = query.SortBy switch
        {
            //  Put undated items last, whichever way the dates run.
            SortField.Date => descending
                ? items.OrderBy(i => i.Date is null).ThenByDescending(i => i.Date)
                : items.OrderBy(i => i.Date is null).ThenBy(i => i.Date),
            SortField.Title => OrderBy(items, i => i.Title, descending, StringComparer.OrdinalIgnoreCase),
            SortField.Source => OrderBy(items, i => i.Source, descending, StringComparer.OrdinalIgnoreCase),
            _ => OrderBy(items, i => i.Relevance, descending),
        };

        //  Break ties the same way every time, so items never move between pages.
        return ordered
            .ThenByDescending(i => i.Relevance)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Source, StringComparer.Ordinal);
    }

    /// <summary>Orders the items by one value, high to low or low to high.</summary>
    private static IOrderedEnumerable<AggregatedItem> OrderBy<TKey>(
        IEnumerable<AggregatedItem> items,
        Func<AggregatedItem, TKey> key,
        bool descending,
        IComparer<TKey>? comparer = null) =>
        descending ? items.OrderByDescending(key, comparer) : items.OrderBy(key, comparer);
}

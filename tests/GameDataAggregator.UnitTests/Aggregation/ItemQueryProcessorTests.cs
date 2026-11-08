using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Models;
using static GameDataAggregator.UnitTests.TestDoubles.TestData;

namespace GameDataAggregator.UnitTests.Aggregation;

/// <summary>Tests filtering, sorting and paging of items.</summary>
public sealed class ItemQueryProcessorTests
{
    private static readonly DateTimeOffset Jan2020 = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan2022 = new(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan2024 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly AggregatedItem[] Items =
    [
        Item("Bravo", "RAWG", ItemCategory.GameInfo, Jan2020, 0.9, "RPG", "Action"),
        Item("Alpha", "CheapShark", ItemCategory.Deal, Jan2024, 0.5, "Steam"),
        Item("Charlie", "Steam", ItemCategory.PlayerActivity, Jan2022, 0.7, "Windows"),
        Item("Delta", "RAWG", ItemCategory.GameInfo, date: null, relevance: 0.6, "rpg"),
    ];

    /// <summary>Creates a query that shows everything on one page, with optional changes.</summary>
    private static AggregationQuery Query(Func<AggregationQuery, AggregationQuery>? configure = null)
    {
        //  Start from a query that keeps everything, then apply the changes.
        var query = new AggregationQuery { Search = "anything", PageSize = 100 };
        return configure is null ? query : configure(query);
    }

    /// <summary>Runs the query over the sample items and returns the titles in order.</summary>
    private static List<string> Titles(AggregationQuery query) =>
        ItemQueryProcessor.Apply(Items, query).Page.Select(i => i.Title).ToList();

    /// <summary>Checks that a plain query returns everything, best match first.</summary>
    [Fact]
    public void Default_query_returns_everything_sorted_by_relevance_descending()
    {
        Assert.Equal(["Bravo", "Charlie", "Delta", "Alpha"], Titles(Query()));
    }

    /// <summary>Checks filtering by source, ignoring letter case.</summary>
    [Fact]
    public void Filters_by_source_case_insensitively()
    {
        //  Ask for RAWG in lower case.
        var query = Query(q => q with { Sources = new HashSet<string>(["rawg"], StringComparer.OrdinalIgnoreCase) });

        //  Expect only RAWG's items.
        Assert.Equal(["Bravo", "Delta"], Titles(query));
    }

    /// <summary>Checks filtering by category.</summary>
    [Fact]
    public void Filters_by_category()
    {
        //  Ask for deals and player activity.
        var query = Query(q => q with { Categories = new HashSet<ItemCategory> { ItemCategory.Deal, ItemCategory.PlayerActivity } });

        //  Expect only those items.
        Assert.Equal(["Charlie", "Alpha"], Titles(query));
    }

    /// <summary>Checks filtering by tag, ignoring letter case.</summary>
    [Fact]
    public void Filters_by_tag_case_insensitively()
    {
        Assert.Equal(["Bravo", "Delta"], Titles(Query(q => q with { Tag = "RPG" })));
    }

    /// <summary>Checks that the date range includes its ends and leaves out undated items.</summary>
    [Fact]
    public void Date_range_is_inclusive_and_excludes_undated_items()
    {
        //  Ask for items from 2020 to 2022.
        var query = Query(q => q with { From = Jan2020, To = Jan2022 });

        //  Expect the items dated exactly on both ends.
        Assert.Equal(["Bravo", "Charlie"], Titles(query));
    }

    /// <summary>Checks that undated items come last when sorting by date either way.</summary>
    [Theory]
    [InlineData(SortDirection.Descending, new[] { "Alpha", "Charlie", "Bravo", "Delta" })]
    [InlineData(SortDirection.Ascending, new[] { "Bravo", "Charlie", "Alpha", "Delta" })]
    public void Sorting_by_date_puts_undated_items_last_in_both_directions(SortDirection direction, string[] expected)
    {
        //  Sort by date in the given direction.
        var query = Query(q => q with { SortBy = SortField.Date, SortDirection = direction });

        //  Expect dated items in order, then the undated one.
        Assert.Equal(expected, Titles(query));
    }

    /// <summary>Checks sorting by title from A to Z.</summary>
    [Fact]
    public void Sorts_by_title_ascending()
    {
        //  Sort by title from A to Z.
        var query = Query(q => q with { SortBy = SortField.Title, SortDirection = SortDirection.Ascending });

        //  Expect alphabetical order.
        Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta"], Titles(query));
    }

    /// <summary>Checks sorting by source, with the best match first within a source.</summary>
    [Fact]
    public void Sorts_by_source_and_breaks_ties_by_relevance()
    {
        //  Sort by source from A to Z.
        var query = Query(q => q with { SortBy = SortField.Source, SortDirection = SortDirection.Ascending });

        //  Expect sources in order, and RAWG's better match before its other item.
        Assert.Equal(["Alpha", "Bravo", "Delta", "Charlie"], Titles(query));
    }

    /// <summary>Checks that paging happens after filtering and sorting, and the total counts every page.</summary>
    [Fact]
    public void Pages_after_filtering_and_sorting_and_reports_total_count()
    {
        //  Ask for the second page of three, by title.
        var query = Query(q => q with { SortBy = SortField.Title, SortDirection = SortDirection.Ascending, Page = 2, PageSize = 3 });

        //  Apply it.
        var (page, total) = ItemQueryProcessor.Apply(Items, query);

        //  Expect the last title alone, and the full total.
        Assert.Equal(4, total);
        Assert.Equal(["Delta"], page.Select(i => i.Title));
    }

    /// <summary>Checks that a page past the end is empty but still reports the total.</summary>
    [Fact]
    public void Page_beyond_the_end_is_empty_but_still_reports_total_count()
    {
        //  Ask for a page far past the end.
        var (page, total) = ItemQueryProcessor.Apply(Items, Query(q => q with { Page = 10, PageSize = 10 }));

        //  Expect no items, but the full total.
        Assert.Empty(page);
        Assert.Equal(4, total);
    }
}

using GameDataAggregator.Core.Aggregation;
using GameDataAggregator.Core.Models;
using GameDataAggregator.UnitTests.TestDoubles;
using static GameDataAggregator.UnitTests.TestDoubles.TestData;

namespace GameDataAggregator.UnitTests.Aggregation;

/// <summary>Tests how the search service combines its sources.</summary>
public sealed class AggregationServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates the service over the given fake sources.</summary>
    private static AggregationService NewService(params FakeGameDataProvider[] providers) =>
        new(providers, NewExecutor());

    /// <summary>Checks that items from all sources are merged and each source is reported.</summary>
    [Fact]
    public async Task Merges_items_from_all_providers_and_reports_each_source()
    {
        //  Set up two sources with one and two items.
        var service = NewService(
            FakeGameDataProvider.Returning("A", Item("The Witcher", "A")),
            FakeGameDataProvider.Returning("B", Item("Witcher 2", "B"), Item("Cyberpunk", "B")));

        //  Search both.
        var result = await service.AggregateAsync(new AggregationQuery { Search = "witcher" }, Ct);

        //  Expect all three items and a live report for each source.
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["A", "B"], result.Sources.Select(s => s.Source));
        Assert.All(result.Sources, s => Assert.Equal(SourceStatus.Live, s.Status));
        Assert.Equal([1, 2], result.Sources.Select(s => s.ItemCount));
    }

    /// <summary>Checks that items are scored against the search.</summary>
    [Fact]
    public async Task Scores_relevance_against_the_search_term()
    {
        //  Set up a source with a matching and a non-matching title.
        var service = NewService(FakeGameDataProvider.Returning("A", Item("Witcher"), Item("Cyberpunk")));

        //  Search it.
        var result = await service.AggregateAsync(new AggregationQuery { Search = "witcher" }, Ct);

        //  Expect the match first with full marks, and the other with none.
        Assert.Equal(["Witcher", "Cyberpunk"], result.Items.Select(i => i.Title));
        Assert.Equal([1.0, 0.0], result.Items.Select(i => i.Relevance));
    }

    /// <summary>Checks that sources are asked at the same time, not one after the other.</summary>
    [Fact]
    public async Task Queries_providers_concurrently()
    {
        //  Make each source wait until both have started, so asking them in turn would time out.
        var started = 0;
        var bothStarted = new TaskCompletionSource();

        //  Mark this source as started, then wait for the other one.
        async Task<IReadOnlyList<AggregatedItem>> WaitForEachOther(string name)
        {
            //  Signal once both sources have started.
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.SetResult();
            }

            //  Wait for the other source, then answer.
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return [Item($"From {name}", name)];
        }

        //  Set up the two waiting sources.
        var service = NewService(
            new FakeGameDataProvider("A", (_, _) => WaitForEachOther("A")),
            new FakeGameDataProvider("B", (_, _) => WaitForEachOther("B")));

        //  Search both.
        var result = await service.AggregateAsync(new AggregationQuery { Search = "x" }, Ct);

        //  Expect both to answer.
        Assert.All(result.Sources, s => Assert.Equal(SourceStatus.Live, s.Status));
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>Checks that only the chosen sources are called.</summary>
    [Fact]
    public async Task Only_calls_the_requested_sources()
    {
        //  Set up two sources.
        var a = FakeGameDataProvider.Returning("A", Item("Witcher", "A"));
        var b = FakeGameDataProvider.Returning("B", Item("Witcher", "B"));
        var service = NewService(a, b);

        //  Search only the second, named in lower case.
        var query = new AggregationQuery { Search = "witcher", Sources = new HashSet<string>(["b"], StringComparer.OrdinalIgnoreCase) };
        var result = await service.AggregateAsync(query, Ct);

        //  Expect only that source to be called and reported.
        Assert.Equal(0, a.Calls);
        Assert.Equal(1, b.Calls);
        Assert.Equal("B", Assert.Single(result.Sources).Source);
    }

    /// <summary>Checks that one failing source does not spoil the others.</summary>
    [Fact]
    public async Task A_failing_provider_does_not_affect_the_others()
    {
        //  Set up a broken source and a working one.
        var service = NewService(
            FakeGameDataProvider.Throwing("Broken", new HttpRequestException("down")),
            FakeGameDataProvider.Returning("Healthy", Item("Witcher", "Healthy")));

        //  Search both.
        var result = await service.AggregateAsync(new AggregationQuery { Search = "witcher" }, Ct);

        //  Expect the working source's item, with the broken one reported as failed.
        Assert.Equal("Healthy", Assert.Single(result.Items).Source);
        Assert.Equal(SourceStatus.Failed, result.Sources.Single(s => s.Source == "Broken").Status);
        Assert.Equal(SourceStatus.Live, result.Sources.Single(s => s.Source == "Healthy").Status);
    }

    /// <summary>Checks that filters and paging apply to the merged items.</summary>
    [Fact]
    public async Task Applies_filters_and_paging_to_the_merged_result()
    {
        //  Set up deals and game info across two sources.
        var service = NewService(
            FakeGameDataProvider.Returning("A", Item("Witcher 1", "A", ItemCategory.Deal), Item("Witcher 2", "A", ItemCategory.GameInfo)),
            FakeGameDataProvider.Returning("B", Item("Witcher 3", "B", ItemCategory.Deal)));

        //  Ask for deals only, one per page, by title from Z to A.
        var query = new AggregationQuery
        {
            Search = "witcher",
            Categories = new HashSet<ItemCategory> { ItemCategory.Deal },
            SortBy = SortField.Title,
            SortDirection = SortDirection.Descending,
            PageSize = 1,
        };
        var result = await service.AggregateAsync(query, Ct);

        //  Expect two deals in total and the last title on the first page.
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("Witcher 3", Assert.Single(result.Items).Title);
        Assert.Equal(1, result.PageSize);
    }

    /// <summary>Checks that the service lists its source names.</summary>
    [Fact]
    public void Exposes_the_registered_source_names()
    {
        //  Set up two sources.
        var service = NewService(FakeGameDataProvider.Returning("A"), FakeGameDataProvider.Returning("B"));

        //  Expect both names, in order.
        Assert.Equal(["A", "B"], service.SourceNames);
    }

    /// <summary>Checks that the service lists each source with its categories, in a fixed order.</summary>
    [Fact]
    public void Lists_each_source_with_its_categories()
    {
        //  Set up a source that gives two categories, declared out of order.
        var source = FakeGameDataProvider.Returning("A");
        source.Categories = new HashSet<ItemCategory> { ItemCategory.PlayerActivity, ItemCategory.GameInfo };

        //  Expect the categories in the order the enum defines them.
        var info = Assert.Single(NewService(source).Sources);
        Assert.Equal("A", info.Name);
        Assert.Equal([ItemCategory.GameInfo, ItemCategory.PlayerActivity], info.Categories);
    }

    /// <summary>Checks that a source that cannot give a chosen category is neither called nor reported.</summary>
    [Fact]
    public async Task Skips_sources_that_cannot_give_a_chosen_category()
    {
        //  Set up a source of game info and a source of player activity.
        var gameInfo = FakeGameDataProvider.Returning("Info", Item("Witcher", "Info", ItemCategory.GameInfo));
        gameInfo.Categories = new HashSet<ItemCategory> { ItemCategory.GameInfo };
        var players = FakeGameDataProvider.Returning("Players", Item("Witcher", "Players", ItemCategory.PlayerActivity));
        players.Categories = new HashSet<ItemCategory> { ItemCategory.PlayerActivity };

        //  Search both for game info only.
        var query = new AggregationQuery { Search = "witcher", Categories = new HashSet<ItemCategory> { ItemCategory.GameInfo } };
        var result = await NewService(gameInfo, players).AggregateAsync(query, Ct);

        //  Expect only the game info source, and no call to the other one.
        Assert.Equal("Info", Assert.Single(result.Sources).Source);
        Assert.Equal(0, players.Calls);
    }

    /// <summary>Checks that each source reports how many of its items pass the filters, on all pages.</summary>
    [Fact]
    public async Task Reports_how_many_items_of_each_source_match_the_filters()
    {
        //  Set up a source with two RPG items out of three, and a source with none.
        var service = NewService(
            FakeGameDataProvider.Returning("A", Item("Witcher", "A", tags: "RPG"), Item("Witcher 2", "A", tags: "RPG"), Item("Witcher 3", "A")),
            FakeGameDataProvider.Returning("B", Item("Witcher", "B")));

        //  Search for RPG items, one per page.
        var result = await service.AggregateAsync(new AggregationQuery { Search = "witcher", Tag = "RPG", PageSize = 1 }, Ct);

        //  Expect what each source found and how much of it matched, counted over all pages.
        Assert.Equal([3, 1], result.Sources.Select(s => s.ItemCount));
        Assert.Equal([2, 0], result.Sources.Select(s => s.MatchCount));
        Assert.Single(result.Items);
    }
}

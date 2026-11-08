using GameDataAggregator.Core.Models;

namespace GameDataAggregator.UnitTests.Models;

/// <summary>Tests what an item says about its date.</summary>
public sealed class AggregatedItemTests
{
    /// <summary>Creates an item with the given date, always marked as a release date.</summary>
    private static AggregatedItem Item(DateTimeOffset? date) => new()
    {
        Source = "Fake",
        Category = ItemCategory.GameInfo,
        Title = "The Witcher",
        Date = date,
        DateKind = DateKind.Released,
    };

    /// <summary>Checks that the meaning of a date is given when there is a date.</summary>
    [Fact]
    public void Date_kind_is_reported_when_there_is_a_date()
    {
        Assert.Equal(DateKind.Released, Item(DateTimeOffset.UnixEpoch).DateKind);
    }

    /// <summary>Checks that no meaning is given without a date, even if a source sets one.</summary>
    [Fact]
    public void Date_kind_is_null_when_there_is_no_date()
    {
        Assert.Null(Item(null).DateKind);
    }

    /// <summary>Checks that removing the date from a copy also removes its meaning.</summary>
    [Fact]
    public void Date_kind_follows_the_date_when_an_item_is_copied()
    {
        //  Copy a dated item without its date.
        var withoutDate = Item(DateTimeOffset.UnixEpoch) with { Date = null };

        //  Expect no meaning to remain.
        Assert.Null(withoutDate.DateKind);
    }
}

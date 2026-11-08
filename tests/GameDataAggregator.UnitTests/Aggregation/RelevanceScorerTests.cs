using GameDataAggregator.Core.Aggregation;

namespace GameDataAggregator.UnitTests.Aggregation;

/// <summary>Tests how titles are scored against a search.</summary>
public sealed class RelevanceScorerTests
{
    /// <summary>Checks that an exact match scores 1, whatever the letter case or punctuation.</summary>
    [Theory]
    [InlineData("the witcher 3", "The Witcher 3")]
    [InlineData("WITCHER", "witcher")]
    [InlineData("witcher 3 wild hunt", "Witcher 3: Wild Hunt")]
    public void Exact_match_ignoring_case_and_punctuation_scores_one(string search, string title)
    {
        Assert.Equal(1.0, RelevanceScorer.Score(search, title));
    }

    /// <summary>Checks that the kinds of match rank in the intended order.</summary>
    [Fact]
    public void Match_types_rank_exact_then_prefix_then_contains_then_word_overlap_then_none()
    {
        //  Score one title for each kind of match.
        var exact = RelevanceScorer.Score("witcher", "Witcher");
        var prefix = RelevanceScorer.Score("witcher", "Witcher Adventure Game");
        var contains = RelevanceScorer.Score("witcher", "The Witcher Adventure Game");
        var someWords = RelevanceScorer.Score("witcher hunt", "The Witcher 3: Wild Hunt");
        var none = RelevanceScorer.Score("witcher", "Cyberpunk 2077");

        //  Expect each to score below the one before, and no match to score 0.
        Assert.True(exact > prefix, $"exact {exact} > prefix {prefix}");
        Assert.True(prefix > contains, $"prefix {prefix} > contains {contains}");
        Assert.True(contains > someWords, $"contains {contains} > words {someWords}");
        Assert.True(someWords > none, $"words {someWords} > none {none}");
        Assert.Equal(0, none);
    }

    /// <summary>Checks that a shorter title beats a longer one with the same kind of match.</summary>
    [Fact]
    public void Shorter_title_scores_higher_for_the_same_match_type()
    {
        //  Score a short and a long title that both contain the search.
        var shortTitle = RelevanceScorer.Score("witcher 3", "The Witcher 3");
        var longTitle = RelevanceScorer.Score("witcher 3", "The Witcher 3: Wild Hunt - Blood and Wine");

        //  Expect the short one to score higher.
        Assert.True(shortTitle > longTitle);
    }

    /// <summary>Checks that partial matches score by the share of words found.</summary>
    [Fact]
    public void Word_overlap_is_proportional_to_matched_words()
    {
        //  Score titles containing half and all of the search words.
        var half = RelevanceScorer.Score("witcher cyberpunk", "The Witcher");
        var all = RelevanceScorer.Score("hunt witcher", "The Witcher 3: Wild Hunt");

        //  Expect half and full credit for word matches.
        Assert.Equal(0.25, half, precision: 6);
        Assert.Equal(0.5, all, precision: 6);
    }

    /// <summary>Checks that empty text scores 0.</summary>
    [Theory]
    [InlineData("", "The Witcher")]
    [InlineData("witcher", "")]
    [InlineData(" : ", "The Witcher")]
    public void Empty_input_scores_zero(string search, string title)
    {
        Assert.Equal(0, RelevanceScorer.Score(search, title));
    }

    /// <summary>Checks that scores never leave the 0 to 1 range.</summary>
    [Fact]
    public void Scores_are_always_between_zero_and_one()
    {
        //  Pick titles of very different lengths and matches.
        string[] titles = ["W", "Witcher", "The Witcher 3: Wild Hunt", "Witcher Witcher Witcher", "x"];

        //  Expect each score to stay within range.
        foreach (var title in titles)
        {
            var score = RelevanceScorer.Score("witcher", title);
            Assert.InRange(score, 0, 1);
        }
    }
}

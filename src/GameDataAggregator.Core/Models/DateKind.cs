namespace GameDataAggregator.Core.Models;

/// <summary>
/// What <see cref="AggregatedItem.Date"/> means for an item. Set by the provider, because sources in the same
/// category can use dates differently (Steam reports when a player count was measured, speedrun.com a release date).
/// </summary>
public enum DateKind
{
    /// <summary>The game's release date.</summary>
    Released,

    /// <summary>When the deal's price last changed.</summary>
    PriceUpdated,

    /// <summary>When the value (e.g. a live player count) was measured.</summary>
    Measured,
}

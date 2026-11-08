namespace GameDataAggregator.Core.Models;

/// <summary>
/// The kind of information an aggregated item carries. Every provider maps its data to one of these.
/// </summary>
public enum ItemCategory
{
    /// <summary>Catalog / design information about a game (genres, ratings, release date).</summary>
    GameInfo,

    /// <summary>A price or discount offered by a store.</summary>
    Deal,

    /// <summary>Live player engagement figures (e.g. concurrent players).</summary>
    PlayerActivity,
}

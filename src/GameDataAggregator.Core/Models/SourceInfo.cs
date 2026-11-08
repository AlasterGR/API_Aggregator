namespace GameDataAggregator.Core.Models;

/// <summary>A registered source and the categories of items it can give.</summary>
/// <param name="Name">The source's name, as used in the <c>sources</c> filter.</param>
/// <param name="Categories">Every category this source's items can have, in a fixed order.</param>
public sealed record SourceInfo(string Name, IReadOnlyList<ItemCategory> Categories)
{
    /// <summary>
    /// Says whether a search uses this source: it must be among the chosen sources (or none were chosen),
    /// and it must give at least one of the chosen categories (or none were chosen).
    /// </summary>
    public bool IsSelectedBy(AggregationQuery query)
    {
        //  Refuse to run without a query.
        ArgumentNullException.ThrowIfNull(query);

        //  Keep the source when it is chosen by name and can give a chosen category.
        var chosenByName = query.Sources.Count == 0 || query.Sources.Contains(Name);
        var givesAChosenCategory = query.Categories.Count == 0 || Categories.Any(query.Categories.Contains);
        return chosenByName && givesAChosenCategory;
    }
}

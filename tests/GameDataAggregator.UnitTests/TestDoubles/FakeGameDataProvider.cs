using GameDataAggregator.Core.Abstractions;
using GameDataAggregator.Core.Models;

namespace GameDataAggregator.UnitTests.TestDoubles;

/// <summary>A provider whose behaviour each test defines, and which counts how often it is called.</summary>
internal sealed class FakeGameDataProvider(
    string name,
    Func<string, CancellationToken, Task<IReadOnlyList<AggregatedItem>>> fetch) : IGameDataProvider
{
    private int _calls;

    public string Name { get; } = name;

    /// <summary>The categories this source gives. All of them unless a test says otherwise.</summary>
    public IReadOnlySet<ItemCategory> Categories { get; set; } = Enum.GetValues<ItemCategory>().ToHashSet();

    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Set to simulate a provider that is missing an API key or other required setting.</summary>
    public string? ConfigurationError { get; set; }

    /// <summary>Creates a source that always returns the given items.</summary>
    public static FakeGameDataProvider Returning(string name, params AggregatedItem[] items) =>
        new(name, (_, _) => Task.FromResult<IReadOnlyList<AggregatedItem>>(items));

    /// <summary>Creates a source that always fails with the given error.</summary>
    public static FakeGameDataProvider Throwing(string name, Exception exception) =>
        new(name, (_, _) => Task.FromException<IReadOnlyList<AggregatedItem>>(exception));

    /// <summary>Counts the call and answers as the test decided.</summary>
    public Task<IReadOnlyList<AggregatedItem>> FetchAsync(string search, CancellationToken cancellationToken)
    {
        //  Count the call, then answer as set up.
        Interlocked.Increment(ref _calls);
        return fetch(search, cancellationToken);
    }
}

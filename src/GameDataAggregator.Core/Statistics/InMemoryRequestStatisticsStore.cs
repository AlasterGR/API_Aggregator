using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace GameDataAggregator.Core.Statistics;

/// <summary>
/// Thread-safe in-memory statistics store, registered as a singleton.
/// <para>
/// Concurrency design: a <see cref="ConcurrentDictionary{TKey,TValue}"/> gives lock-free lookup of the
/// per-API counters, and each API's counters are guarded by their own small lock. The lock makes an
/// update (count + total time + bucket) atomic, so a snapshot can never observe a half-applied
/// update. Contention is minimal: the critical section is a few additions and requests to
/// different APIs never wait on each other.
/// </para>
/// <para>
/// Besides the totals since start-up, each API keeps one small count per clock minute for the last
/// <see cref="MaxWindow"/>, in a ring of slots that are reused as time moves on. That gives a recent view for the
/// performance monitor at a fixed, tiny memory cost, whatever the traffic.
/// </para>
/// </summary>
public sealed class InMemoryRequestStatisticsStore(
    IOptions<StatisticsOptions> options,
    TimeProvider timeProvider) : IRequestStatisticsStore
{
    /// <summary>The longest recent window that can be asked for. It is also how many minute slots each API keeps.</summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromMinutes(60);

    private static readonly PerformanceBucket[] AllBuckets = Enum.GetValues<PerformanceBucket>();
    private static readonly int MinuteSlotCount = (int)MaxWindow.TotalMinutes;

    private readonly ConcurrentDictionary<string, ApiCounters> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly StatisticsOptions _options = options.Value;

    /// <summary>Adds one request to its source's counts.</summary>
    public void Record(string api, TimeSpan duration, bool isSuccess)
    {
        //  Refuse a blank source name.
        ArgumentException.ThrowIfNullOrWhiteSpace(api);

        //  Add the request to the source's counts, creating them on first use.
        var counters = _counters.GetOrAdd(api, static name => new ApiCounters(name));
        counters.Add(Classify(duration), duration, isSuccess, CurrentMinute());
    }

    /// <summary>Returns the current figures for every source, in name order.</summary>
    public IReadOnlyList<ApiStatistics> GetSnapshot(IEnumerable<string>? knownApis = null)
    {
        //  Include known sources that have had no requests yet.
        foreach (var api in knownApis ?? [])
        {
            _counters.GetOrAdd(api, static name => new ApiCounters(name));
        }

        //  Turn each source's counts into figures, sorted by name.
        return _counters.Values
            .Select(c => c.ToStatistics(_options))
            .OrderBy(s => s.Api, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns each called source's average inside the recent window and before it, in name order.</summary>
    public IReadOnlyList<ApiPerformanceWindow> GetPerformanceWindows(TimeSpan window)
    {
        //  Accept only whole minutes that the minute slots can cover.
        if (window < TimeSpan.FromMinutes(1) || window > MaxWindow || window.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, $"The window must be whole minutes, from 1 to {MaxWindow.TotalMinutes}.");
        }

        //  Split each source's figures at the start of the window, sorted by name.
        var currentMinute = CurrentMinute();
        var minutes = (int)window.TotalMinutes;
        return _counters.Values
            .Select(c => c.ToPerformanceWindow(currentMinute, minutes))
            .Where(w => w.RecentRequests + w.BaselineRequests > 0)
            .OrderBy(w => w.Api, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Numbers clock minutes since 1970, so each minute has its own slot number.</summary>
    private long CurrentMinute() => timeProvider.GetUtcNow().ToUnixTimeSeconds() / 60;

    /// <summary>Puts a duration in the fast, average or slow group.</summary>
    internal PerformanceBucket Classify(TimeSpan duration) =>
        duration.TotalMilliseconds < _options.FastThresholdMs ? PerformanceBucket.Fast
        : duration.TotalMilliseconds <= _options.SlowThresholdMs ? PerformanceBucket.Average
        : PerformanceBucket.Slow;

    /// <summary>One source's running counts, kept consistent by a lock.</summary>
    private sealed class ApiCounters(string api)
    {
        private readonly Lock _gate = new();
        private readonly long[] _requests = new long[AllBuckets.Length];
        private readonly TimeSpan[] _totalDuration = new TimeSpan[AllBuckets.Length];
        private readonly MinuteSlot[] _minutes = new MinuteSlot[MinuteSlotCount];
        private long _failed;

        /// <summary>Counts one request in its speed group and in its clock minute.</summary>
        public void Add(PerformanceBucket bucket, TimeSpan duration, bool isSuccess, long minute)
        {
            //  Update all counts together, so readers never see half an update.
            lock (_gate)
            {
                _requests[(int)bucket]++;
                _totalDuration[(int)bucket] += duration;
                if (!isSuccess)
                {
                    _failed++;
                }

                //  Reuse the minute's slot, clearing it first when it still holds an older minute.
                ref var slot = ref _minutes[minute % MinuteSlotCount];
                if (slot.Minute != minute)
                {
                    slot = new MinuteSlot { Minute = minute };
                }

                slot.Requests++;
                slot.Duration += duration;
            }
        }

        /// <summary>Splits the counts into the last <paramref name="minutes"/> clock minutes and everything before.</summary>
        public ApiPerformanceWindow ToPerformanceWindow(long currentMinute, int minutes)
        {
            long totalRequests;
            TimeSpan totalDuration;
            long recentRequests = 0;
            var recentDuration = TimeSpan.Zero;

            //  Read the totals and the window's slots under the lock, so both describe the same moment.
            lock (_gate)
            {
                totalRequests = _requests.Sum();
                totalDuration = _totalDuration.Aggregate(TimeSpan.Zero, (sum, d) => sum + d);

                //  Add up only the slots that belong to the window, skipping slots left from older minutes.
                foreach (var slot in _minutes)
                {
                    if (slot.Requests > 0 && slot.Minute > currentMinute - minutes && slot.Minute <= currentMinute)
                    {
                        recentRequests += slot.Requests;
                        recentDuration += slot.Duration;
                    }
                }
            }

            //  Treat everything outside the window as the baseline.
            var baselineRequests = totalRequests - recentRequests;
            return new ApiPerformanceWindow
            {
                Api = api,
                RecentRequests = recentRequests,
                RecentAverageResponseTimeMs = Average(recentDuration, recentRequests),
                BaselineRequests = baselineRequests,
                BaselineAverageResponseTimeMs = Average(totalDuration - recentDuration, baselineRequests),
            };
        }

        /// <summary>Turns the counts into figures for the statistics endpoint.</summary>
        public ApiStatistics ToStatistics(StatisticsOptions options)
        {
            long[] requests;
            TimeSpan[] durations;
            long failed;

            //  Copy the counts under the lock, and do the maths outside it.
            lock (_gate)
            {
                requests = (long[])_requests.Clone();
                durations = (TimeSpan[])_totalDuration.Clone();
                failed = _failed;
            }

            //  Add up all requests and time across the groups.
            var totalRequests = requests.Sum();
            var totalDuration = durations.Aggregate(TimeSpan.Zero, (sum, d) => sum + d);

            //  Build the figures, overall and per speed group.
            return new ApiStatistics
            {
                Api = api,
                TotalRequests = totalRequests,
                FailedRequests = failed,
                AverageResponseTimeMs = Average(totalDuration, totalRequests),
                Buckets = AllBuckets.Select(b => new BucketStatistics
                {
                    Bucket = b,
                    Range = DescribeRange(b, options),
                    Requests = requests[(int)b],
                    AverageResponseTimeMs = Average(durations[(int)b], requests[(int)b]),
                }).ToList(),
            };
        }

        /// <summary>Average time per request, or 0 when there were none.</summary>
        private static double Average(TimeSpan total, long count) =>
            count == 0 ? 0 : Math.Round(total.TotalMilliseconds / count, 2);

        /// <summary>Describes a speed group's range in words.</summary>
        private static string DescribeRange(PerformanceBucket bucket, StatisticsOptions options) => bucket switch
        {
            PerformanceBucket.Fast => $"< {options.FastThresholdMs} ms",
            PerformanceBucket.Average => $"{options.FastThresholdMs}-{options.SlowThresholdMs} ms",
            _ => $"> {options.SlowThresholdMs} ms",
        };
    }

    /// <summary>Requests and total time in one clock minute.</summary>
    private struct MinuteSlot
    {
        /// <summary>Which minute the slot holds. Slots start at 0, which is far in the past.</summary>
        public long Minute;

        public long Requests;

        public TimeSpan Duration;
    }
}

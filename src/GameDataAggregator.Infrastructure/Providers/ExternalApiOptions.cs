using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers;

/// <summary>
/// Settings shared by every external API. Each provider's options class derives from this and is bound
/// from "ExternalApis:{Provider}" in configuration.
/// </summary>
public abstract class ExternalApiOptions : IValidatableObject
{
    /// <summary>Disabled providers are not registered and never called.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum number of items requested from the API per search.</summary>
    [Range(1, 50)]
    public int MaxResults { get; set; } = 10;

    /// <summary>Timeout for a single HTTP attempt.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Timeout for the whole request, including all retries.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Retries for transient failures (5xx, 408, 429, network errors), with exponential back-off and jitter.</summary>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Checks that the overall time limit is longer than the limit per attempt.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        //  Reject an overall limit that leaves no room for a retry.
        if (TotalTimeout <= AttemptTimeout)
        {
            yield return new ValidationResult(
                $"{nameof(TotalTimeout)} must be greater than {nameof(AttemptTimeout)}.",
                [nameof(TotalTimeout)]);
        }
    }
}

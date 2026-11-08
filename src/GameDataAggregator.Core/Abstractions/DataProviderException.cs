namespace GameDataAggregator.Core.Abstractions;

/// <summary>
/// A provider failure whose message is safe to show to API callers (for example a missing API key).
/// Any other exception is reported to callers with a generic message and logged in full.
/// </summary>
public sealed class DataProviderException : Exception
{
    /// <summary>Creates the error with a message callers may see.</summary>
    public DataProviderException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the error with a message callers may see and the error that caused it.</summary>
    public DataProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

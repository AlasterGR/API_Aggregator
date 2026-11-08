using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDataAggregator.Infrastructure.Providers;

/// <summary>Small helpers shared by the providers' mapping code.</summary>
internal static class ProviderMapping
{
    /// <summary>For APIs using snake_case property names (RAWG, Steam).</summary>
    public static readonly JsonSerializerOptions SnakeCaseJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>For APIs using camelCase property names; also accepts numbers sent as strings (CheapShark).</summary>
    public static readonly JsonSerializerOptions CamelCaseJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Builds a metrics dictionary, leaving out values the API did not provide.</summary>
    public static Dictionary<string, double> Metrics(params (string Key, double? Value)[] values) =>
        values.Where(v => v.Value.HasValue).ToDictionary(v => v.Key, v => v.Value!.Value);

    /// <summary>Builds an attributes dictionary, leaving out empty values.</summary>
    public static Dictionary<string, string> Attributes(params (string Key, string? Value)[] values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToDictionary(v => v.Key, v => v.Value!);

    /// <summary>Turns text into a web address, or nothing if it is not a full address.</summary>
    public static Uri? ToUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}

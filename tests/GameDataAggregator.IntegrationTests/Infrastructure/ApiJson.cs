using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDataAggregator.IntegrationTests.Infrastructure;

/// <summary>Matches the API's JSON settings so responses can be deserialised into the real models.</summary>
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

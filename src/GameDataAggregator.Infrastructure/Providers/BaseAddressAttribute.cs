using System.ComponentModel.DataAnnotations;

namespace GameDataAggregator.Infrastructure.Providers;

/// <summary>
/// Checks that a setting is a full http or https address whose path ends with "/".
/// Without the "/", .NET drops the last path segment when a relative path is added
/// (for example "https://api.rawg.io/api" plus "games" gives "https://api.rawg.io/games").
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BaseAddressAttribute : ValidationAttribute
{
    /// <summary>Sets the message shown when the check fails.</summary>
    public BaseAddressAttribute()
        : base("{0} must be a full http or https address that ends with '/', for example 'https://api.example.com/v1/'.")
    {
    }

    /// <summary>Returns true for an empty value or a full address ending with "/".</summary>
    public override bool IsValid(object? value) => value switch
    {
        //  Leave a missing value to the [Required] check.
        null => true,

        //  Accept only a full web address whose path ends with "/".
        Uri uri => uri.IsAbsoluteUri
                   && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                   && uri.AbsolutePath.EndsWith('/'),

        //  Refuse anything that is not an address.
        _ => false,
    };
}

using System.ComponentModel.DataAnnotations;
using GameDataAggregator.Infrastructure.Providers.CheapShark;
using GameDataAggregator.Infrastructure.Providers.Igdb;
using GameDataAggregator.Infrastructure.Providers.Rawg;
using GameDataAggregator.Infrastructure.Providers.Speedrun;
using GameDataAggregator.Infrastructure.Providers.Steam;

namespace GameDataAggregator.UnitTests.Infrastructure;

/// <summary>Tests the checks on each source's settings.</summary>
public sealed class ExternalApiOptionsTests
{
    /// <summary>Runs every settings check and says whether they all pass.</summary>
    private static bool IsValid(object options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);

    /// <summary>Checks that every source's default settings are valid.</summary>
    [Fact]
    public void Defaults_are_valid_for_every_provider()
    {
        Assert.True(IsValid(new RawgOptions()));
        Assert.True(IsValid(new CheapSharkOptions()));
        Assert.True(IsValid(new SteamOptions()));
        Assert.True(IsValid(new IgdbOptions()));
        Assert.True(IsValid(new SpeedrunOptions()));
    }

    /// <summary>Checks that a base address must be a full web address ending with "/".</summary>
    [Theory]
    [InlineData("https://api.rawg.io/api")]
    [InlineData("api.rawg.io/api/")]
    [InlineData("ftp://api.rawg.io/api/")]
    public void Base_url_must_be_a_full_web_address_ending_with_a_slash(string baseUrl)
    {
        //  Read the address the way the settings loader would, which also accepts relative addresses.
        var uri = new Uri(baseUrl, UriKind.RelativeOrAbsolute);

        //  Expect the address to be refused for a main and a side base address.
        Assert.False(IsValid(new RawgOptions { BaseUrl = uri }));
        Assert.False(IsValid(new SteamOptions { WebApiBaseUrl = uri }));
    }

    /// <summary>Checks that a base address with only a host and no path is accepted.</summary>
    [Fact]
    public void Base_url_with_only_a_host_is_valid()
    {
        Assert.True(IsValid(new SteamOptions { WebApiBaseUrl = new Uri("https://api.steampowered.com") }));
    }

    /// <summary>Checks that the overall time limit must be longer than the limit per attempt.</summary>
    [Fact]
    public void Total_timeout_must_exceed_attempt_timeout()
    {
        //  Set both limits to the same length.
        var options = new SteamOptions { AttemptTimeout = TimeSpan.FromSeconds(10), TotalTimeout = TimeSpan.FromSeconds(10) };

        //  Expect the settings to be refused.
        Assert.False(IsValid(options));
    }

    /// <summary>Checks that the result limit must stay within its range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Max_results_must_be_in_range(int maxResults)
    {
        Assert.False(IsValid(new RawgOptions { MaxResults = maxResults }));
    }

    /// <summary>Checks that Steam's country code must have two letters.</summary>
    [Fact]
    public void Steam_country_code_must_have_two_letters()
    {
        Assert.False(IsValid(new SteamOptions { CountryCode = "USA" }));
    }
}

namespace GameDataAggregator.Api.Contracts;

/// <summary>Response of <c>POST /api/v1/auth/token</c>, shaped like an OAuth 2.0 token response.</summary>
public sealed record TokenResponse
{
    /// <summary>The token, to send as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Always "Bearer".</summary>
    public required string TokenType { get; init; }

    /// <summary>Seconds until the token expires.</summary>
    public required int ExpiresIn { get; init; }

    /// <summary>When the token expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

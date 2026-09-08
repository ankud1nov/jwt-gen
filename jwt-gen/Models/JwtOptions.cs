namespace jwt_gen.Models;

/// <summary>Параметры JWT-токена.</summary>
public sealed class JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public string? Subject { get; init; }
    public int ExpiresInMinutes { get; init; } = 60;
    public IReadOnlyDictionary<string, string> Claims { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

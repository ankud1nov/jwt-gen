namespace jwt_gen.Models;

public sealed class JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public string? Subject { get; init; }
    public int? ExpiresInMinutes { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Claims { get; init; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
}

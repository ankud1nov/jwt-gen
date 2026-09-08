using System.Security.Claims;
using System.Security.Cryptography;
using jwt_gen.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace jwt_gen.Services;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    public string Generate(string privateKeyPem, JwtOptions options)
    {
        if (options.ExpiresInMinutes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Срок действия должен быть больше нуля минут.");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var claims = options.Claims
            .SelectMany(pair => pair.Value.Select(value => new Claim(pair.Key, value)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(options.Subject))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, options.Subject));
        }

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = options.ExpiresInMinutes is { } expiresInMinutes
                ? now.AddMinutes(expiresInMinutes)
                : null,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256)
        };

        return new JsonWebTokenHandler
        {
            SetDefaultTimesOnTokenCreation = false
        }.CreateToken(descriptor);
    }
}

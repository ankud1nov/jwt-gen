using System.Security.Cryptography;
using jwt_gen.Models;
using jwt_gen.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace jwt_gen.Tests;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void Generate_WithoutExpiration_DoesNotAddExpClaim()
    {
        var token = GenerateToken(expiresInMinutes: null);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Exp);
        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Iat);
        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Nbf);
    }

    [Fact]
    public void Generate_WithExpiration_AddsExpClaim()
    {
        var token = GenerateToken(expiresInMinutes: 60);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Exp);
    }

    [Fact]
    public void Generate_WithNonPositiveExpiration_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GenerateToken(expiresInMinutes: 0));
    }

    private static string GenerateToken(int? expiresInMinutes)
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

        return new JwtTokenGenerator().Generate(
            privateKeyPem,
            new JwtOptions
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                ExpiresInMinutes = expiresInMinutes
            });
    }
}

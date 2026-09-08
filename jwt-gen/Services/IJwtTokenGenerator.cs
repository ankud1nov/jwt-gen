using jwt_gen.Models;

namespace jwt_gen.Services;

/// <summary>Создаёт подписанные RSA JWT-токены.</summary>
public interface IJwtTokenGenerator
{
    /// <summary>Создаёт JWT, подписанный приватным RSA-ключом.</summary>
    string Generate(string privateKeyPem, JwtOptions options);
}

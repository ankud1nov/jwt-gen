using jwt_gen.Models;

namespace jwt_gen.Services;

/// <summary>Создаёт и загружает RSA-ключи для подписи JWT.</summary>
public interface IKeyPairService
{
    /// <summary>Генерирует новую пару RSA-ключей.</summary>
    RsaKeyPair Generate(int keySize);

    /// <summary>Строит публичный ключ из приватного PEM-ключа.</summary>
    string ExportPublicKey(string privateKeyPem);

    /// <summary>Проверяет, что приватный и публичный PEM-ключи образуют одну пару.</summary>
    bool ValidateKeyPair(string privateKeyPem, string publicKeyPem);
}

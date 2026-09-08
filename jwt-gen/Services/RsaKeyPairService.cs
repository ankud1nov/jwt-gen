using System.Security.Cryptography;
using jwt_gen.Models;

namespace jwt_gen.Services;

public sealed class RsaKeyPairService : IKeyPairService
{
    public RsaKeyPair Generate(int keySize)
    {
        if (keySize < 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(keySize), "The RSA key size must be at least 2048 bits.");
        }

        using var rsa = RSA.Create(keySize);
        return new RsaKeyPair(
            rsa.ExportPkcs8PrivateKeyPem(),
            rsa.ExportSubjectPublicKeyInfoPem());
    }

    public string ExportPublicKey(string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }

    public bool ValidateKeyPair(string privateKeyPem, string publicKeyPem)
    {
        try
        {
            using var privateRsa = RSA.Create();
            privateRsa.ImportFromPem(privateKeyPem);

            using var publicRsa = RSA.Create();
            publicRsa.ImportFromPem(publicKeyPem);

            var privateParameters = privateRsa.ExportParameters(includePrivateParameters: false);
            var publicParameters = publicRsa.ExportParameters(includePrivateParameters: false);

            return privateParameters.Modulus.AsSpan().SequenceEqual(publicParameters.Modulus) &&
                   privateParameters.Exponent.AsSpan().SequenceEqual(publicParameters.Exponent);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

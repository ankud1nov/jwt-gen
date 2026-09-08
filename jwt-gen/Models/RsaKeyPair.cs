namespace jwt_gen.Models;

/// <summary>Пара RSA-ключей в PEM-представлении.</summary>
public sealed record RsaKeyPair(string PrivateKeyPem, string PublicKeyPem);

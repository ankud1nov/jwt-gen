using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using jwt_gen.Models;
using jwt_gen.Services;

namespace jwt_gen;

internal static class Program
{
    private const int DefaultRsaKeySize = 2048;
    private const string DefaultIssuer = "jwt-gen";
    private const string DefaultAudience = "api";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                return RunInteractive();
            }

            var parseResult = BuildCommandLine().Parse(args);
            return parseResult.Invoke(new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = true
            });
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException or IOException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static RootCommand BuildCommandLine()
    {
        var root = new RootCommand("Generate and manage RSA-signed JWTs.");
        var generate = new Command("generate", "Generate an RSA key pair and a JWT.");
        var issuer = new Option<string?>("--issuer") { Description = "Token issuer." };
        var audience = new Option<string?>("--audience") { Description = "Token audience." };
        var subject = new Option<string?>("--subject") { Description = "Token subject." };
        var expires = new Option<int?>("--expires") { Description = "Token lifetime in minutes. Omit for unlimited." };
        var claim = new Option<string[]>("--claim") { Description = "Additional claim in name=value format." };
        var keySize = new Option<int?>("--key-size") { Description = "RSA key size in bits." };
        var privateKey = new Option<string?>("--private-key") { Description = "Use an existing private PEM key." };
        var savePrivate = new Option<string?>("--save-private") { Description = "Save the private key to this path." };
        var savePublic = new Option<string?>("--save-public") { Description = "Save the public key to this path." };

        generate.Options.Add(issuer);
        generate.Options.Add(audience);
        generate.Options.Add(subject);
        generate.Options.Add(expires);
        generate.Options.Add(claim);
        generate.Options.Add(keySize);
        generate.Options.Add(privateKey);
        generate.Options.Add(savePrivate);
        generate.Options.Add(savePublic);
        generate.SetAction(parseResult => Generate(new GenerateOptions(
            parseResult.GetValue(issuer) ?? DefaultIssuer,
            parseResult.GetValue(audience) ?? DefaultAudience,
            parseResult.GetValue(subject),
            parseResult.GetValue(expires),
            ParseClaims(parseResult.GetValue(claim)),
            parseResult.GetValue(keySize) ?? DefaultRsaKeySize,
            parseResult.GetValue(privateKey),
            parseResult.GetValue(savePrivate),
            parseResult.GetValue(savePublic))));

        var publicKey = new Command("public-key", "Derive a public key from a private PEM key.");
        var publicKeyPrivatePath = new Option<string?>("--private-key")
        {
            Description = "Path to the private PEM key.",
            Required = true
        };
        var publicKeySavePath = new Option<string?>("--save-public") { Description = "Save the public PEM key to this path." };
        publicKey.Options.Add(publicKeyPrivatePath);
        publicKey.Options.Add(publicKeySavePath);
        publicKey.SetAction(parseResult => GeneratePublicKey(
            RequireValue(parseResult.GetValue(publicKeyPrivatePath), "--private-key"),
            parseResult.GetValue(publicKeySavePath)));

        var validateKey = new Command("validate-key", "Validate a private and public key pair.");
        var validatePrivatePath = new Option<string?>("--private-key")
        {
            Description = "Path to the private PEM key.",
            Required = true
        };
        var validatePublicPath = new Option<string?>("--public-key")
        {
            Description = "Path to the public PEM key.",
            Required = true
        };
        validateKey.Options.Add(validatePrivatePath);
        validateKey.Options.Add(validatePublicPath);
        validateKey.SetAction(parseResult => ValidateKey(
            RequireValue(parseResult.GetValue(validatePrivatePath), "--private-key"),
            RequireValue(parseResult.GetValue(validatePublicPath), "--public-key")));

        root.Subcommands.Add(generate);
        root.Subcommands.Add(publicKey);
        root.Subcommands.Add(validateKey);
        return root;
    }

    private static void Generate(GenerateOptions options)
    {
        if (options.ExpiresInMinutes is { } expiresInMinutes)
        {
            ValidatePositive(expiresInMinutes, "--expires");
        }
        ValidatePositive(options.KeySize, "--key-size");

        var keyService = new RsaKeyPairService();
        var keyPair = string.IsNullOrWhiteSpace(options.PrivateKeyPath)
            ? keyService.Generate(options.KeySize)
            : LoadKeys(options.PrivateKeyPath, keyService);

        var token = new JwtTokenGenerator().Generate(
            keyPair.PrivateKeyPem,
            new JwtOptions
            {
                Issuer = options.Issuer,
                Audience = options.Audience,
                Subject = options.Subject,
                ExpiresInMinutes = options.ExpiresInMinutes,
                Claims = options.Claims
            });

        WriteIfSpecified(options.PrivateKeyPathToSave, keyPair.PrivateKeyPem);
        WriteIfSpecified(options.PublicKeyPath, keyPair.PublicKeyPem);

        Console.WriteLine("=== Private RSA key (keep it secret) ===");
        Console.WriteLine(keyPair.PrivateKeyPem);
        Console.WriteLine("=== Public RSA key ===");
        Console.WriteLine(keyPair.PublicKeyPem);
        Console.WriteLine("=== JWT ===");
        Console.WriteLine(token);
    }

    private static RsaKeyPair LoadKeys(string privateKeyPath, IKeyPairService keyService)
    {
        var privateKeyPem = File.ReadAllText(privateKeyPath);
        return new RsaKeyPair(privateKeyPem, keyService.ExportPublicKey(privateKeyPem));
    }

    private static void GeneratePublicKey(string privateKeyPath, string? savePublicPath)
    {
        var keyService = new RsaKeyPairService();
        var publicKeyPem = keyService.ExportPublicKey(File.ReadAllText(privateKeyPath));
        WriteIfSpecified(savePublicPath, publicKeyPem);
        Console.WriteLine(publicKeyPem);
    }

    private static void ValidateKey(string privateKeyPath, string publicKeyPath)
    {
        var keyService = new RsaKeyPairService();
        var isValid = keyService.ValidateKeyPair(
            File.ReadAllText(privateKeyPath),
            File.ReadAllText(publicKeyPath));

        Console.WriteLine(isValid
            ? "The public key matches the private key."
            : "The public key does NOT match the private key.");

        if (!isValid)
        {
            Environment.ExitCode = 1;
        }
    }

    private static int RunInteractive()
    {
        Console.WriteLine("jwt-gen - RSA JWT generator");
        Console.WriteLine();
        Console.WriteLine("1. Generate JWT");
        Console.WriteLine("2. Derive public key");
        Console.WriteLine("3. Validate key pair");
        Console.WriteLine("0. Exit");
        Console.WriteLine();

        return ReadLine("Select an action", "1") switch
        {
            "1" => RunInteractiveGenerate(),
            "2" => RunInteractivePublicKey(),
            "3" => RunInteractiveValidateKey(),
            "0" => 0,
            _ => throw new ArgumentException("Unknown menu option.")
        };
    }

    private static int RunInteractiveGenerate()
    {
        var privateKeyPath = ReadOptionalPath("Existing private key path (leave empty to generate a new key)");
        var options = new GenerateOptions(
            ReadLine("Issuer", DefaultIssuer),
            ReadLine("Audience", DefaultAudience),
            ReadOptional("Subject"),
            ReadOptionalPositiveInt("Token lifetime in minutes"),
            ReadClaims(),
            ReadPositiveInt("RSA key size in bits", DefaultRsaKeySize),
            privateKeyPath,
            ReadOptionalPath("Save private key to"),
            ReadOptionalPath("Save public key to"));

        Generate(options);
        return 0;
    }

    private static int RunInteractivePublicKey()
    {
        GeneratePublicKey(
            ReadRequired("Private key path"),
            ReadOptionalPath("Save public key to"));
        return 0;
    }

    private static int RunInteractiveValidateKey()
    {
        ValidateKey(ReadRequired("Private key path"), ReadRequired("Public key path"));
        return 0;
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> ReadClaims()
    {
        var claims = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        while (ReadLine("Add a claim? (y/N)", "N").Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            var name = ReadRequired("Claim name");
            AddClaim(claims, name, ReadLine("Claim value", string.Empty));
        }

        return ToReadOnlyClaims(claims);
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> ParseClaims(string[]? values)
    {
        var claims = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var value in values ?? [])
        {
            var separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException("Each --claim value must use the name=value format.");
            }

            AddClaim(claims, value[..separator], value[(separator + 1)..]);
        }

        return ToReadOnlyClaims(claims);
    }

    private static void AddClaim(IDictionary<string, List<string>> claims, string name, string value)
    {
        if (!claims.TryGetValue(name, out var values))
        {
            values = [];
            claims[name] = values;
        }

        values.Add(value);
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> ToReadOnlyClaims(
        IReadOnlyDictionary<string, List<string>> claims) =>
        claims.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<string>)pair.Value,
            StringComparer.Ordinal);

    private static string ReadLine(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var value = Console.ReadLine();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            var value = ReadLine(prompt, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Console.WriteLine("A value is required.");
        }
    }

    private static string? ReadOptional(string prompt)
    {
        var value = ReadLine(prompt, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadOptionalPath(string prompt)
    {
        Console.Write($"{prompt} [optional]: ");
        var value = Console.ReadLine();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int ReadPositiveInt(string prompt, int defaultValue)
    {
        while (true)
        {
            var value = ReadLine(prompt, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
            {
                return result;
            }

            Console.WriteLine("Enter a positive integer.");
        }
    }

    private static int? ReadOptionalPositiveInt(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt} [unlimited]: ");
            var value = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
            {
                return result;
            }

            Console.WriteLine("Enter a positive integer or leave empty for an unlimited token.");
        }
    }

    private static void ValidatePositive(int value, string option)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{option} must be a positive integer.");
        }
    }

    private static string RequireValue(string? value, string option) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"The {option} option is required.")
            : value;

    private static void WriteIfSpecified(string? path, string content)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, content);
            Console.WriteLine($"Saved: {Path.GetFullPath(path)}");
        }
    }

    private sealed record GenerateOptions(
        string Issuer,
        string Audience,
        string? Subject,
        int? ExpiresInMinutes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> Claims,
        int KeySize,
        string? PrivateKeyPath,
        string? PrivateKeyPathToSave,
        string? PublicKeyPath);
}

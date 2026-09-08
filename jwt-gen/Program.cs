using System.Globalization;
using System.Security.Cryptography;
using jwt_gen.Models;
using jwt_gen.Services;

namespace jwt_gen;

internal static class Program
{
    private const int DefaultRsaKeySize = 2048;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || HasOption(args, "--help") || HasOption(args, "-h"))
            {
                PrintHelp();
                return 0;
            }

            if (string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
            {
                Generate(args[1..]);
                return 0;
            }

            if (string.Equals(args[0], "public-key", StringComparison.OrdinalIgnoreCase))
            {
                GeneratePublicKey(args[1..]);
                return 0;
            }

            if (string.Equals(args[0], "validate-key", StringComparison.OrdinalIgnoreCase))
            {
                ValidateKey(args[1..]);
                return 0;
            }

            throw new ArgumentException($"Неизвестная команда '{args[0]}'. Используйте 'jwt-gen --help'.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            Console.Error.WriteLine("Используйте 'jwt-gen --help' для справки.");
            return 1;
        }
    }

    private static void Generate(string[] args)
    {
        var options = new CommandLineOptions(args);
        var keyService = new RsaKeyPairService();
        var tokenGenerator = new JwtTokenGenerator();
        var keyPair = LoadOrGenerateKeys(options, keyService);

        var token = tokenGenerator.Generate(
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

        Console.WriteLine("=== Приватный RSA-ключ (храните в секрете) ===");
        Console.WriteLine(keyPair.PrivateKeyPem);
        Console.WriteLine("=== Публичный RSA-ключ ===");
        Console.WriteLine(keyPair.PublicKeyPem);
        Console.WriteLine("=== JWT ===");
        Console.WriteLine(token);
    }

    private static RsaKeyPair LoadOrGenerateKeys(CommandLineOptions options, IKeyPairService keyService)
    {
        if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
        {
            return keyService.Generate(options.KeySize);
        }

        var privateKeyPem = File.ReadAllText(options.PrivateKeyPath);
        return new RsaKeyPair(privateKeyPem, keyService.ExportPublicKey(privateKeyPem));
    }

    private static void GeneratePublicKey(string[] args)
    {
        var privateKeyPath = GetRequiredOption(args, "--private-key");
        var savePublicPath = GetOption(args, "--save-public");
        var keyService = new RsaKeyPairService();
        var publicKeyPem = keyService.ExportPublicKey(File.ReadAllText(privateKeyPath));

        WriteIfSpecified(savePublicPath, publicKeyPem);
        Console.WriteLine(publicKeyPem);
    }

    private static void ValidateKey(string[] args)
    {
        var privateKeyPath = GetRequiredOption(args, "--private-key");
        var publicKeyPath = GetRequiredOption(args, "--public-key");
        var keyService = new RsaKeyPairService();
        var isValid = keyService.ValidateKeyPair(
            File.ReadAllText(privateKeyPath),
            File.ReadAllText(publicKeyPath));

        Console.WriteLine(isValid
            ? "Публичный ключ соответствует приватному."
            : "Публичный ключ НЕ соответствует приватному.");

        if (!isValid)
        {
            Environment.ExitCode = 1;
        }
    }

    private static void WriteIfSpecified(string? path, string content)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(path, content);
            Console.WriteLine($"Сохранено: {Path.GetFullPath(path)}");
        }
    }

    private static bool HasOption(string[] args, string option) =>
        args.Any(arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));

    private static void PrintHelp()
    {
        Console.WriteLine("Генератор RSA JWT");
        Console.WriteLine();
        Console.WriteLine("Использование:");
        Console.WriteLine("  jwt-gen generate [параметры]");
        Console.WriteLine("  jwt-gen public-key --private-key <путь> [--save-public <путь>]");
        Console.WriteLine("  jwt-gen validate-key --private-key <путь> --public-key <путь>");
        Console.WriteLine();
        Console.WriteLine("Параметры:");
        Console.WriteLine("  --issuer <значение>       Issuer - кто выпустил токен. (по умолчанию: jwt-gen)");
        Console.WriteLine("  --audience <значение>     Audience — для какого сервиса предназначен токен. (по умолчанию: api)");
        Console.WriteLine("  --subject <значение>      Subject — идентификатор пользователя или субъекта токена. (Например: user-123)");
        Console.WriteLine("  --expires <минуты>        Срок действия JWT в минутах. (по умолчанию: 60)");
        Console.WriteLine("  --claim name=value        Дополнительный claim; можно указывать несколько раз");
        Console.WriteLine("  --key-size <бит>          Размер нового RSA-ключа (по умолчанию: 2048)");
        Console.WriteLine("  --private-key <путь>      Использовать существующий приватный PEM-ключ");
        Console.WriteLine("  --save-private <путь>     Сохранить приватный ключ в файл");
        Console.WriteLine("  --save-public <путь>      Сохранить публичный ключ в файл");
        Console.WriteLine("  --help                    Показать эту справку");
    }

    private static string? GetOption(string[] args, string option)
    {
        var index = Array.FindIndex(args, arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Для параметра {option} требуется значение.");
        }

        return args[index + 1];
    }

    private static string GetRequiredOption(string[] args, string option) =>
        GetOption(args, option) ?? throw new ArgumentException($"Необходимо указать параметр {option}.");

    private sealed class CommandLineOptions
    {
        public CommandLineOptions(string[] args)
        {
            var claims = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index++)
            {
                var option = args[index];
                switch (option)
                {
                    case "--issuer": Issuer = ReadValue(args, ref index, option); break;
                    case "--audience": Audience = ReadValue(args, ref index, option); break;
                    case "--subject": Subject = ReadValue(args, ref index, option); break;
                    case "--expires": ExpiresInMinutes = ParsePositiveInt(ReadValue(args, ref index, option), option); break;
                    case "--key-size": KeySize = ParsePositiveInt(ReadValue(args, ref index, option), option); break;
                    case "--claim": AddClaim(claims, ReadValue(args, ref index, option)); break;
                    case "--private-key": PrivateKeyPath = ReadValue(args, ref index, option); break;
                    case "--save-private": PrivateKeyPathToSave = ReadValue(args, ref index, option); break;
                    case "--save-public": PublicKeyPath = ReadValue(args, ref index, option); break;
                    default: throw new ArgumentException($"Неизвестный параметр '{option}'.");
                }
            }

            Claims = claims;
        }

        public string Issuer { get; } = "jwt-gen";
        public string Audience { get; } = "api";
        public string? Subject { get; }
        public int ExpiresInMinutes { get; } = 60;
        public int KeySize { get; } = DefaultRsaKeySize;
        public string? PrivateKeyPath { get; }
        public string? PrivateKeyPathToSave { get; }
        public string? PublicKeyPath { get; }
        public IReadOnlyDictionary<string, string> Claims { get; }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Для параметра {option} требуется значение.");
            }

            return args[index];
        }

        private static int ParsePositiveInt(string value, string option) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0
                ? result
                : throw new ArgumentException($"Параметр {option} должен быть положительным целым числом.");

        private static void AddClaim(IDictionary<string, string> claims, string value)
        {
            var separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException("Claim должен иметь формат name=value.");
            }

            claims[value[..separator]] = value[(separator + 1)..];
        }
    }
}

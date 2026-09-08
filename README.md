# jwt-gen

A small command-line utility for generating RSA key pairs and JWTs signed with a private key.

## Usage

```powershell
dotnet run --project .\jwt-gen -- generate `
  --issuer MyIssuer `
  --audience MyApi `
  --subject user-123 `
  --expires 60 `
  --claim role=admin `
  --save-private .\private.pem `
  --save-public .\public.pem
```

When `--private-key .\private.pem` is specified, a new key pair is not generated. The public key is derived from the existing private key, and the JWT is signed with it.

Never publish or share the private key with API consumers. Consumers only need the public key to verify the signature.

To use the interactive wizard instead of command-line options, run the application without arguments:

```powershell
dotnet run --project .\jwt-gen
```

The wizard provides a menu for generating a JWT, deriving a public key, or validating a key pair. For command-line usage, the application provides structured help and validation through `System.CommandLine`:

```powershell
dotnet run --project .\jwt-gen -- --help
dotnet run --project .\jwt-gen -- generate --help
```

Use `--claim name=value` more than once to add multiple claims.

## Build and publish

Build the project normally:

```powershell
dotnet build .\jwt-gen.sln -c Release
```

Publish a standalone Windows executable that does not require the .NET Runtime:

```powershell
dotnet publish .\jwt-gen\jwt-gen.csproj `
  -c Release `
  -r win-x64 `
  -o .\publish\win-x64
```

The publish settings are defined in the project file. The command only specifies the build configuration, target runtime, and output directory.

The resulting file will be available at:

```text
publish\win-x64\jwt-gen.exe
```

It can be copied to another 64-bit Windows computer and run directly:

```powershell
.\publish\win-x64\jwt-gen.exe generate `
  --issuer MyIssuer `
  --audience MyApi `
  --subject user-123 `
  --claim role=admin
```

For ARM64, use `-r win-arm64` and a separate output directory, for example `-o .\publish\win-arm64`. Do not mix publishes for different architectures in the same directory.

The project uses Native AOT (`PublishAot`) and the modern `Microsoft.IdentityModel.JsonWebTokens` package, which supports trimming and Native AOT. As a result, the final `jwt-gen.exe` is significantly smaller than a self-contained .NET application and does not require the .NET Runtime.

## Public key and key-pair validation

Derive a public key from an existing private key:

```powershell
dotnet run --project .\jwt-gen -- public-key `
  --private-key .\private.pem `
  --save-public .\public.pem
```

Verify that the public key matches the private key:

```powershell
dotnet run --project .\jwt-gen -- validate-key `
  --private-key .\private.pem `
  --public-key .\public.pem
```

## Key distribution and revocation

You do not need to regenerate the private key to publish the public key again. Run `public-key` and publish the resulting PEM file in the JWT verification service.

However, revoking a public key does not revoke JWTs that have already been issued. Any token signed with the same private key remains cryptographically valid. Use a short token lifetime and a server-side denylist based on `jti` or the user, or check the user's session version.

For true key rotation and immediate revocation, generate a new RSA key pair, start signing tokens with the new private key, publish both public keys during the transition period, and then remove the old public key. The old private key may be kept in an archive, but must not be used to sign new tokens.

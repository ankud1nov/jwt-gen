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

Use `--claim name=value` more than once to add multiple claims. Repeating the same claim name writes its values as a JSON array in the JWT payload:

```powershell
dotnet run --project .\jwt-gen -- generate `
  --claim scope=read:hello-world `
  --claim scope=write:hello-world
```

The resulting payload contains:

```json
"scope": ["read:hello-world", "write:hello-world"]
```

By default, generated tokens have no expiration (`exp`). To limit a token lifetime, pass a positive number of minutes explicitly, for example `--expires 60`. In the interactive wizard, leave `Token lifetime in minutes [unlimited]` empty to generate a token without an expiration.

The receiving API must be configured to accept tokens without `exp`. Unlimited tokens should be used with care because they remain valid until their signing key is revoked.

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

## Manual releases

Native AOT applications must be published on the target operating system. Build the Windows artifact on Windows, the Linux artifact on Linux, and the macOS artifacts on the corresponding macOS architecture.

Before creating a release, make sure all release changes are committed and pushed, and verify GitHub CLI authentication:

```powershell
git status
git push origin main
gh auth status
```

The following examples build version `0.1.0` and use the Git tag `v0.1.0`.

### Windows x64

Run on Windows:

```powershell
dotnet publish .\jwt-gen\jwt-gen.csproj `
  -c Release `
  -r win-x64 `
  -p:Version=0.1.0 `
  -o .\publish\win-x64

New-Item -ItemType Directory -Force .\publish\release | Out-Null
Compress-Archive `
  -Path .\publish\win-x64\jwt-gen.exe `
  -DestinationPath .\publish\release\jwt-gen-v0.1.0-win-x64.zip `
  -Force
```

### Linux x64

Run on Linux:

```bash
dotnet publish ./jwt-gen/jwt-gen.csproj \
  -c Release \
  -r linux-x64 \
  -p:Version=0.1.0 \
  -o ./publish/linux-x64

mkdir -p ./publish/release
tar -C ./publish/linux-x64 \
  -czf ./publish/release/jwt-gen-v0.1.0-linux-x64.tar.gz \
  jwt-gen
```

### macOS Apple Silicon

Run on an Apple Silicon Mac:

```bash
dotnet publish ./jwt-gen/jwt-gen.csproj \
  -c Release \
  -r osx-arm64 \
  -p:Version=0.1.0 \
  -o ./publish/osx-arm64

mkdir -p ./publish/release
tar -C ./publish/osx-arm64 \
  -czf ./publish/release/jwt-gen-v0.1.0-osx-arm64.tar.gz \
  jwt-gen
```

### macOS Intel

Run on an Intel Mac:

```bash
dotnet publish ./jwt-gen/jwt-gen.csproj \
  -c Release \
  -r osx-x64 \
  -p:Version=0.1.0 \
  -o ./publish/osx-x64

mkdir -p ./release
tar -C ./publish/osx-x64 \
  -czf ./release/jwt-gen-v0.1.0-osx-x64.tar.gz \
  jwt-gen
```

Copy all archives into `publish/release` on the machine where GitHub CLI is authenticated. Then create and push the annotated tag:

```powershell
git add <нужные-файлы>
git commit -m "Release v0.1.0"
git tag -a v0.1.0 -m "jwt-gen v0.1.0"
git push origin v0.1.0
```

Create the GitHub release and upload all archives:

```powershell
gh release create v0.1.0 .\publish\release\* `
  --verify-tag `
  --title "jwt-gen v0.1.0" `
  --generate-notes
```

Verify the published release:

```powershell
gh release view v0.1.0 --web
```

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

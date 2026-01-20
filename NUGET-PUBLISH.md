# NuGet Publishing Guide

## Package Information

- **Package ID**: CSharperMcp
- **Description**: MCP server for LLMs to understand C# code without brute-force grepping & hallucinations
- **Type**: .NET Global Tool
- **Command**: `csharper-mcp`

## Building the Package

```bash
# Build the NuGet package
dotnet pack src/CSharperMcp.Server/CSharperMcp.Server.csproj -c Release -o ./artifacts

# The package will be created at: ./artifacts/CSharperMcp.{version}.nupkg
```

## Local Installation (Testing)

Before publishing, you can test the package locally:

```bash
# Install from local package
dotnet tool install --global --add-source ./artifacts CSharperMcp

# Run the tool
csharper-mcp

# Uninstall when done testing
dotnet tool uninstall -g CSharperMcp
```

## Publishing to NuGet.org

### Prerequisites

1. Create an account at [nuget.org](https://www.nuget.org/)
2. Generate an API key:
   - Go to your account settings
   - Navigate to "API Keys"
   - Create a new API key with "Push" permissions
   - Select package ID pattern (e.g., `CSharperMcp*`)

### Manual Publishing

```bash
# Push to NuGet.org
dotnet nuget push ./artifacts/CSharperMcp.0.1.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

# Wait for validation (usually a few minutes)
# Package will appear at: https://www.nuget.org/packages/CSharperMcp
```

### Version Management

Update the version in [src/CSharperMcp.Server/CSharperMcp.Server.csproj](src/CSharperMcp.Server/CSharperMcp.Server.csproj):

```xml
<Version>0.1.0</Version>  <!-- Update this for new releases -->
```

Follow semantic versioning:
- **Major** (1.0.0): Breaking changes
- **Minor** (0.1.0): New features, backwards compatible
- **Patch** (0.0.1): Bug fixes

### Pre-release Versions

For preview releases:

```xml
<Version>0.2.0-preview.1</Version>
```

## User Installation

Once published, users can install with:

```bash
# Install the tool globally
dotnet tool install --global CSharperMcp

# Run it
csharper-mcp

# Update to latest version
dotnet tool update --global CSharperMcp

# Uninstall
dotnet tool uninstall --global CSharperMcp
```

## Automated Publishing with GitHub Actions

To automate publishing on git tags, add `.github/workflows/publish.yml`:

```yaml
name: Publish NuGet Package

on:
  push:
    tags:
      - 'v*'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build -c Release --no-restore

    - name: Pack
      run: dotnet pack src/CSharperMcp.Server/CSharperMcp.Server.csproj -c Release -o ./artifacts

    - name: Push to NuGet
      run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

Then add your API key as a GitHub secret named `NUGET_API_KEY`.

To publish a new version:

```bash
git tag v0.1.0
git push origin v0.1.0
```

## Package Contents

The package includes:
- CSharperMcp.Server executable
- All dependencies (Roslyn, ILSpy, MCP SDK, etc.)
- README.md
- License information

## Support

For issues or questions:
- GitHub: https://github.com/diskodan/CSharperMcp/issues
- NuGet Package: https://www.nuget.org/packages/CSharperMcp

# CSharper Mcp

An MCP (Model Context Protocol) server that provides IDE-like semantic understanding of C# code using Roslyn and ILSpy.

## Features

- **Workspace Management**: Load and analyze C# solutions and projects
- **Diagnostics**: Get compiler errors, warnings, and analyzer messages
- **Symbol Intelligence**: Navigate definitions, find references, inspect types
- **DLL Introspection**: Decompile and explore third-party assemblies
- **Code Actions**: Apply refactorings and quick fixes

## Getting Started

```bash
# Build the project
dotnet build

# Run the server
dotnet run --project src/CSharperMcp.Server

# Run tests
dotnet test
```

## Project Structure

```
csharp-er-mcp/
├── Directory.Build.props              # Shared build properties
├── Directory.Packages.props           # Central package management
├── src/
│   └── CSharperMcp.Server/          # Main MCP server
│       ├── Program.cs                # Entry point
│       ├── Server/Tools/             # MCP tool implementations
│       ├── Workspace/                # Solution/project management
│       ├── Services/                 # Roslyn & Decompiler services
│       └── Models/                   # Data models
└── tests/
    ├── CSharperMcp.Server.UnitTests/           # Fast unit tests
    └── CSharperMcp.Server.IntegrationTests/    # Slower integration tests
```

## Technology Stack

- **.NET 10**: Latest C# language features
- **ModelContextProtocol**: Official MCP C# SDK
- **Roslyn**: Microsoft.CodeAnalysis for semantic analysis
- **ILSpy**: ICSharpCode.Decompiler for DLL introspection
- **NUnit 4**: Testing framework with instance-per-test-fixture
- **FluentAssertions**: Readable test assertions
- **Moq**: Mocking framework

## Requirements

- .NET 10.0 SDK
- MSBuild (automatically located via Microsoft.Build.Locator)

## Development

### Build Configuration

The project uses:
- **Central Package Management** via `Directory.Packages.props`
- **InternalsVisibleTo** automatically configured for test projects
- **Latest C# language features** enabled
- **Nullable reference types** enabled throughout

### Testing

Tests use NUnit with instance-per-test-fixture lifecycle:

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test tests/CSharperMcp.Server.UnitTests

# Run only integration tests
dotnet test tests/CSharperMcp.Server.IntegrationTests
```

## MCP Tools

### Phase 1: Foundation
- `initialize_workspace` - Load solution or project
- `get_diagnostics` - Retrieve compiler diagnostics

### Coming Soon
- Symbol information and navigation
- Find references
- Code actions and refactorings
- NuGet package management

# CSharperMcp

An MCP (Model Context Protocol) server that gives LLMs IDE-like semantic understanding of C# code. Stop grepping files and hallucinating code structure—use Roslyn and ILSpy to provide real compiler diagnostics, symbol navigation, find references, decompiled DLLs, and code actions.

## Why This Exists

Current AI coding tools (Claude Code, Cursor) lack C# language server integration. Without it, LLMs grep files and brute-force reverse engineer code structure. This is slow, misses semantic context, and completely fails for code in DLLs. This server fixes that by wrapping Roslyn APIs and ICSharpCode.Decompiler.

## Features

- **Workspace Management**: Load and analyze C# solutions and projects
- **Diagnostics**: Get compiler errors, warnings, and analyzer messages
- **Symbol Intelligence**: Navigate definitions, find references, inspect types
- **DLL Introspection**: Decompile and explore third-party assemblies and NuGet packages
- **Code Actions**: Apply refactorings and quick fixes

---

## Installation

The server is distributed as a .NET global tool via NuGet. Use `dnx` (dotnet execute) to run it without explicit installation:

```bash
# Run directly via dnx (no installation needed)
dnx CSharperMcp

# Or install globally
dotnet tool install --global CSharperMcp

# Run after installing
csharper-mcp
```

### Requirements

- **.NET 10.0 SDK or later**
- **MSBuild** (automatically located via Microsoft.Build.Locator)

---

## Configuration

### Using with Claude Code

Add to your project's `.mcp.json` or your global MCP configuration:

```json
{
  "mcpServers": {
    "csharp-er-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["--yes", "CSharperMcp", "--workspace-from-cwd"]
    }
  }
}
```

**Recommended:** Use `--workspace-from-cwd` which automatically uses the current working directory. This allows the tool to be installed globally and work across all your C# projects without per-project configuration.

**Alternative:** If you need to specify an explicit workspace path, use `--workspace /some/folder`:

```json
{
  "mcpServers": {
    "csharp-er-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["--yes", "CSharperMcp", "--workspace", "/path/to/your/csharp/project"]
    }
  }
}
```

**Note:** Variable expansion like `"${workspaceFolder}"` doesn't work reliably with Claude Code's global tool installations, which is why `--workspace-from-cwd` is recommended.

### Using with Cursor

**Recommended:** Use `--workspace-from-cwd` for global installation across all projects:

```json
{
  "mcpServers": {
    "csharp-er-mcp": {
      "command": "dnx",
      "args": ["--yes", "CSharperMcp", "--workspace-from-cwd"]
    }
  }
}
```

**Alternative:** If you need to specify an explicit workspace path, you can use `${workspaceFolder}` variable expansion (this works in Cursor):

```json
{
  "mcpServers": {
    "csharp-er-mcp": {
      "command": "dnx",
      "args": ["--yes", "CSharperMcp", "--workspace", "${workspaceFolder}"]
    }
  }
}
```

### Other MCP Clients

```bash
dnx --yes CSharperMcp --workspace /path/to/your/project
```

### Workspace Auto-initialization

The server supports two ways to automatically initialize a workspace on startup:

**`--workspace <path>`** - Specify an explicit path to initialize:
```bash
dnx --yes CSharperMcp --workspace /path/to/your/project
```

**`--workspace-from-cwd`** - Use the current working directory:
```bash
dnx --yes CSharperMcp --workspace-from-cwd
```

Both options will:
1. Look for `.sln` files (prefer one matching directory name, else largest)
2. Fall back to `.csproj` files if no solution found
3. Load the workspace and make tools available immediately
4. Disable the `initialize_workspace` tool (restart the server to change workspaces)

**Without either parameter**: You must call the `initialize_workspace` tool manually after connecting.

### Advanced Configuration with YAML

You can customize server behavior and tool descriptions using YAML configuration files. The server supports hierarchical configuration merging:

**Configuration file locations** (later files override earlier):

1. `~/.config/csharp-er-mcp.yml` - User global preferences (optional)
2. `<workspace>/.config/csharp-er-mcp.yml` - Project-specific config (optional)

**Example configuration:**

```yaml
# Server configuration (optional - has defaults)
mcp:
  serverInstructions: "Custom instructions for the MCP server..."

# Tool descriptions (optional)
tools:
  initialize_workspace:
    description: "Custom description here..."
  get_diagnostics:
  some_tool_to_hide:
    isEnabled: false # Tool will be filtered out from MCP client
```

**Features:**

- Override server instructions sent to MCP clients
- Customize individual tool descriptions
- Disable specific tools via `isEnabled: false`
- Configuration merging: user config → project config → command-line overrides

---

## Available MCP Tools

### Foundation Tools

#### `initialize_workspace`

Load a C# solution or project from a path.

**Input:**

```json
{
  "path": "/absolute/path/to/solution/or/project/or/directory"
}
```

**Returns:** Project count, solution path, any load errors.

**Note:** Disabled when `--workspace` or `--workspace-from-cwd` is provided.

---

#### `get_diagnostics`

Get compiler errors, warnings, and analyzer messages.

**Input:**

```json
{
  "file": "/path/to/file.cs", // Optional: filter by file
  "startLine": 10, // Optional: filter by line range
  "endLine": 50, // Optional: filter by line range
  "severity": "Error" // Optional: Error|Warning|Info|Hidden
}
```

**Returns:** Array of diagnostics with:

- Diagnostic ID (e.g., `CS0103`)
- Severity (Error, Warning, Info, Hidden)
- Message
- File path, line, column, end line, end column
- `hasFix` boolean (whether a code action can fix this)

**No arguments**: Returns diagnostics for entire workspace.

---

### Symbol Intelligence Tools (Coming Soon)

#### `get_symbol_info`

Get type information at a location or by fully qualified name.

#### `find_symbol_usages`

Find all references to a symbol across the workspace.

#### `get_definition_location`

Navigate to the definition of a symbol (in workspace or DLL metadata).

#### `get_type_members`

Get full type definition with all members (decompiled for DLL types).

---

### Code Action Tools (Coming Soon)

#### `get_code_actions`

Get available refactorings and quick fixes at a location.

#### `apply_code_action`

Execute a code action (with optional preview before applying).

---

### Search & Navigation Tools (Coming Soon)

#### `search_symbols`

Find symbols by name pattern (supports camelCase matching).

#### `get_document_symbols`

Get hierarchical outline of a file's symbols.

---

### Extension Methods (Special Feature, Coming Soon)

#### `get_extension_methods`

Find all extension methods available for a given type (scans all referenced assemblies).

---

## Usage Examples

See [CLAUDE.md](CLAUDE.md#usage-examples) for comprehensive examples showing how to:

- Initialize workspace and fix compiler errors
- Navigate symbols and find usages
- Introspect NuGet package code (decompiled DLLs)
- Apply refactorings with preview
- Discover extension methods

---

## Development

### Prerequisites

- .NET 10.0 SDK
- MSBuild (automatically located)

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build everything
dotnet build

# Run the server locally
dotnet run --project src/CSharperMcp.Server

# Run all tests
dotnet test

# Run only unit tests (fast)
dotnet test tests/CSharperMcp.Server.UnitTests

# Run only integration tests (slower, multi-process)
dotnet test tests/CSharperMcp.Server.IntegrationTests
```

### Building the NuGet Package

```bash
# Build the package
dotnet pack src/CSharperMcp.Server/CSharperMcp.Server.csproj -c Release -o ./artifacts

# Test locally before publishing
dotnet tool install --global --add-source ./artifacts CSharperMcp
```

See [NUGET-PUBLISH.md](NUGET-PUBLISH.md) for publishing instructions.

---

## Project Structure

```
csharp-er-mcp/
├── Directory.Build.props              # Shared build configuration
├── Directory.Packages.props           # Central package version management
├── CSharperMcp.sln                    # Solution file
├── src/
│   └── CSharperMcp.Server/            # Main MCP server console app
│       ├── Program.cs                 # Entry point (MSBuildLocator.RegisterDefaults())
│       ├── Server/Tools/              # MCP tool implementations
│       ├── Workspace/                 # Solution/project loading & management
│       ├── Services/                  # Roslyn & Decompiler service wrappers
│       └── Models/                    # DTOs for tool responses
└── tests/
    ├── CSharperMcp.Server.UnitTests/           # Fast unit tests
    └── CSharperMcp.Server.IntegrationTests/    # Slower integration tests
```

---

## Technology Stack

- **.NET 10** - Latest C# language features
- **ModelContextProtocol 0.6.0** - Official MCP C# SDK
- **Microsoft.CodeAnalysis.Workspaces.MSBuild 5.0.0** - Roslyn APIs
- **Microsoft.Build.Locator 1.11.2** - Find MSBuild dynamically
- **ICSharpCode.Decompiler 9.1.0** - ILSpy engine for DLL introspection
- **NUnit 4** - Testing framework
- **Moq** - Mocking framework
- **FluentAssertions** - Fluent assertion library

---

## Development Guidelines

See [CLAUDE.md](CLAUDE.md) for comprehensive development instructions including:

- Code style and access modifiers (default to `internal`)
- Central Package Management requirements
- Testing philosophy (unit vs integration)
- Architecture patterns
- MCP tool implementation details

---

## Support

- **Issues**: [GitHub Issues](https://github.com/diskodan/CSharperMcp/issues)
- **NuGet Package**: [CSharperMcp on NuGet.org](https://www.nuget.org/packages/CSharperMcp)

---

## License

See [LICENSE](LICENSE) file for details.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CSharperMcp - C# LSP MCP Server

## Project Summary

Build an MCP (Model Context Protocol) server in C# that provides LLMs with IDE-like semantic understanding of C# code. The server wraps Roslyn APIs and ICSharpCode.Decompiler to give LLMs the same intelligence that JetBrains Rider or VS Code provides - symbol info, find references, go to definition, diagnostics, code actions, and crucially, the ability to introspect third-party DLLs and NuGet packages.

## Why This Exists

Current AI coding tools (Claude Code, Cursor) lack C# language server integration. Without it, LLMs grep files and brute-force reverse engineer code structure. This is slow, misses semantic context, and completely fails for code in DLLs. This server fixes that.

---

## Development Commands

```bash
# Build everything
dotnet build

# Run the server
dotnet run --project src/CSharperMcp.Server

# Run all tests
dotnet test

# Run only unit tests (fast)
dotnet test tests/CSharperMcp.Server.UnitTests

# Run only integration tests (slower, multi-process)
dotnet test tests/CSharperMcp.Server.IntegrationTests

# Restore packages
dotnet restore
```

---

## Project Structure

```
csharp-er-mcp/
├── Directory.Build.props              # Shared build configuration
├── Directory.Packages.props           # Central package version management
├── CSharperMcp.sln                  # Solution file
├── src/
│   └── CSharperMcp.Server/          # Main MCP server console app
│       ├── Program.cs                # Entry point (CRITICAL: MSBuildLocator.RegisterDefaults())
│       ├── Server/Tools/             # MCP tool implementations
│       ├── Workspace/                # Solution/project loading & management
│       ├── Services/                 # Roslyn & Decompiler service wrappers
│       └── Models/                   # DTOs for tool responses
└── tests/
    ├── CSharperMcp.Server.UnitTests/           # Fast unit tests
    └── CSharperMcp.Server.IntegrationTests/    # Slower integration tests
```

---

## Development Guidelines

### Code Style & Access Modifiers

- **Default to `internal`**: All classes, records, and interfaces should be `internal` unless they need to be `public` for external consumption. The test projects have automatic `InternalsVisibleTo` access.

- **Use latest C# features**: The project targets .NET 10 with `<LangVersion>latest</LangVersion>`. Use modern C# patterns (records, pattern matching, file-scoped namespaces, etc.).

- **Nullable reference types enabled**: All code must respect nullable annotations. Never use `!` suppression unless absolutely necessary.

### Central Package Management

**CRITICAL**: This project uses Central Package Management (CPM). All package versions are defined in `Directory.Packages.props`.

- ✅ Correct: `<PackageReference Include="Moq" />`
- ❌ Wrong: `<PackageReference Include="Moq" Version="4.20.72" />`

When adding new packages:
1. Add version to `Directory.Packages.props`
2. Reference without version in `.csproj` files

### Build Configuration

The `Directory.Build.props` file automatically:
- Enables nullable reference types
- Uses latest C# language version
- Treats warnings as errors in Release builds
- Configures `InternalsVisibleTo` for test projects
- Includes Moq's DynamicProxyGenAssembly2 for mocking internal types

### Testing Philosophy

This project uses **NUnit 4** with `FixtureLifeCycle.InstancePerTestCase` (configured in `GlobalUsings.cs`). Each test gets a fresh instance of the test fixture.

#### Unit Tests (`CSharperMcp.Server.UnitTests`)
- Fast, isolated tests
- Mock all external dependencies
- Use `Moq` for mocking
- Use `FluentAssertions` for assertions
- Test individual classes/methods in isolation

#### Integration Tests (`CSharperMcp.Server.IntegrationTests`)
- Slower, end-to-end tests
- Test real Roslyn workspace loading
- Test against real C# projects
- Can be multi-process
- Should cover common use cases comprehensively

**CRITICAL**: Integration tests are the primary defense against regressions. They should cover:
- Loading real solutions (.sln files)
- Loading real projects (.csproj files)
- Getting diagnostics from projects with errors
- Getting diagnostics from projects with warnings
- Symbol resolution in user code
- Symbol resolution in NuGet packages
- Decompiling types from DLLs
- Find references across projects
- Code actions and refactorings

The goal: **Drop this into Cursor and have it "just work"™**. Integration tests prevent surprises.

#### Test Fixtures Should:
```csharp
[TestFixture]
public class MyServiceTests
{
    private Mock<IDependency> _mockDependency = null!;
    private MyService _sut = null!; // System Under Test

    [SetUp]
    public void SetUp()
    {
        _mockDependency = new Mock<IDependency>();
        _sut = new MyService(_mockDependency.Object);
    }

    [TearDown]
    public void TearDown()
    {
        (_sut as IDisposable)?.Dispose();
    }

    [Test]
    public void MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        _mockDependency.Setup(x => x.DoSomething()).Returns(42);

        // Act
        var result = _sut.DoWork();

        // Assert
        result.Should().Be(42);
        _mockDependency.Verify(x => x.DoSomething(), Times.Once);
    }
}
```

---

## Tech Stack

- **.NET 10** - Latest C# language features
- **ModelContextProtocol 0.6.0-preview.1** - Official MCP C# SDK
- **Microsoft.CodeAnalysis.Workspaces.MSBuild 5.0.0** - Roslyn APIs
- **Microsoft.Build.Locator 1.11.2** - Find MSBuild dynamically
- **ICSharpCode.Decompiler 9.1.0.7988** - ILSpy engine for DLL introspection
- **NUnit 4.3.1** - Testing framework with instance-per-test-fixture
- **Moq 4.20.72** - Mocking framework
- **FluentAssertions 6.12.2** - Fluent assertion library

---

## Architecture

### Entry Point (`Program.cs`)

**CRITICAL**: `MSBuildLocator.RegisterDefaults()` MUST be called before any Roslyn types are loaded.

```csharp
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

// CRITICAL: Register MSBuild before ANY Roslyn types are loaded
MSBuildLocator.RegisterDefaults();

var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddSingleton<WorkspaceManager>();
builder.Services.AddSingleton<RoslynService>();
builder.Services.AddSingleton<DecompilerService>();

// Register tool classes
builder.Services.AddTransient<WorkspaceTool>();
builder.Services.AddTransient<DiagnosticsTool>();

// Register MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

await builder.Build().RunAsync();
```

### Key Components

- **WorkspaceManager** (`Workspace/WorkspaceManager.cs`): Loads `.sln` or `.csproj` files, manages Roslyn workspace lifecycle
- **RoslynService** (`Services/RoslynService.cs`): Wraps Roslyn APIs for diagnostics, symbol lookup, semantic analysis
- **DecompilerService** (`Services/DecompilerService.cs`): Wraps ICSharpCode.Decompiler for DLL introspection
- **Tool Classes** (`Server/Tools/*.cs`): Implement MCP tools, injected into MCP server via DI

---

## MCP Tools to Implement

### Phase 1: Foundation (✅ COMPLETE)

1. **`initialize_workspace`** - Load solution/project from path
   - Input: `{ "path": "/abs/path/to/sln/or/csproj/or/dir" }`
   - Find `.sln` (prefer root, then largest), fall back to `.csproj`
   - Call `MSBuildWorkspace.Create()` then `OpenSolutionAsync()`
   - Return: project count, any load errors

2. **`get_diagnostics`** - Compiler errors/warnings/analyzer messages
   - Input: `{ "file?", "startLine?", "endLine?", "severity?" }`
   - No args = entire workspace; file = just that file; lines = range filter
   - Use `Compilation.GetDiagnostics()` or `SemanticModel.GetDiagnostics()`
   - Return: array of `{ id, message, severity, file, line, column, endLine, endColumn, hasFix }`

### Phase 2: Symbol Intelligence

3. **`get_symbol_info`** - Type info at location or by name
   - Input: `{ "file", "line", "column" }` OR `{ "symbolName": "Fully.Qualified.Name" }`
   - Use `SemanticModel.GetSymbolInfo()` or `Compilation.GetTypeByMetadataName()`
   - Return: kind, name, containingType, namespace, assembly, package, docComment, modifiers, signature

4. **`find_references`** - All usages of a symbol
   - Input: `{ "file", "line", "column" }` OR `{ "symbolName" }`
   - Use `SymbolFinder.FindReferencesAsync()`
   - Return: array of `{ file, line, column, contextSnippet, referenceKind }`

5. **`get_definition`** - Go to definition (source or decompiled)
   - Input: `{ "file", "line", "column" }` OR `{ "symbolName" }`
   - If in workspace: return file location
   - If in DLL: use ICSharpCode.Decompiler to get source
   - Return: `{ file, line, column }` OR `{ decompiledSource, assembly, package }`

6. **`get_type_members`** - Full type definition with all members
   - Input: `{ "typeName": "System.String", "includeInherited?": false }`
   - For workspace types: use Roslyn
   - For DLL types: use Decompiler
   - Return: full decompiled source or structured member list

### Phase 3: Code Actions

7. **`get_code_actions`** - Available refactorings/fixes at location
   - Input: `{ "file", "line", "column", "endLine?", "endColumn?", "diagnosticIds?": ["CS0001"] }`
   - Use `CodeFixService` and `CodeRefactoringService`
   - Return: array of `{ id, title, kind }`

8. **`apply_code_action`** - Execute a code action
   - Input: `{ "actionId", "file", "preview?": true }`
   - If preview: return diff
   - If not preview: apply changes to workspace, return modified files
   - Return: `{ changes: [{ file, diff }] }` or `{ applied: true, modifiedFiles: [] }`

### Phase 4: Search & Navigation

9. **`search_symbols`** - Find symbols by name pattern
   - Input: `{ "query", "kinds?": ["class","method"], "maxResults?": 50 }`
   - Support camelCase matching (e.g., "GD" matches "GetDefault")
   - Return: array of `{ name, kind, file, line, containerName }`

10. **`get_document_symbols`** - File outline/structure
    - Input: `{ "file" }`
    - Return: hierarchical symbol tree

### Phase 5: Extension Methods (special feature)

11. **`get_extension_methods`** - Find extensions for a type
    - Input: `{ "typeName": "IDictionary<,>", "includeDocumentation?": true }`
    - Scan all referenced assemblies for extension methods on that type
    - Include XML doc comments
    - Return: array of extension method signatures with docs

---

## Key Implementation Patterns

### Loading a Solution

```csharp
var workspace = MSBuildWorkspace.Create();
workspace.RegisterWorkspaceFailedHandler(diagnostic =>
    _logger.LogWarning("Workspace diagnostic: {Message}", diagnostic.Message));
var solution = await workspace.OpenSolutionAsync(solutionPath);
```

### Getting Diagnostics

```csharp
foreach (var project in solution.Projects)
{
    var compilation = await project.GetCompilationAsync();
    var diagnostics = compilation.GetDiagnostics()
        .Where(d => d.Severity >= DiagnosticSeverity.Warning);
    // Map to response DTOs
}
```

### Symbol at Location

```csharp
var document = solution.GetDocument(documentId);
var semanticModel = await document.GetSemanticModelAsync();
var position = GetPositionFromLineColumn(text, line, column);
var symbolInfo = semanticModel.GetSymbolInfo(syntaxNode);
```

### Decompiling DLL Types

```csharp
var decompiler = new CSharpDecompiler(assemblyPath, new DecompilerSettings
{
    ThrowOnAssemblyResolveErrors = false
});
var typeName = new FullTypeName("Namespace.TypeName");
var decompiledSource = decompiler.DecompileTypeAsString(typeName);
```

---

## Integration Testing Requirements

**Philosophy**: Integration tests are the primary safety net. They should cover real-world scenarios comprehensively to ensure the server works when dropped into Cursor/Claude Code.

### Test Fixtures Needed

Create test fixtures in `tests/CSharperMcp.Server.IntegrationTests/Fixtures/`:

1. **SimpleSolution** - Single project, no dependencies
   ```
   ├── SimpleSolution.sln
   └── SimpleProject/
       ├── SimpleProject.csproj
       ├── Program.cs (no errors)
       └── Helper.cs (no errors)
   ```

2. **SolutionWithErrors** - Multiple projects with intentional errors
   ```
   ├── ErrorSolution.sln
   ├── ProjectA/
   │   ├── ProjectA.csproj
   │   └── ClassWithError.cs (CS0103: undeclared variable)
   └── ProjectB/
       ├── ProjectB.csproj (references ProjectA)
       └── AnotherError.cs (CS0246: missing using directive)
   ```

3. **SolutionWithNuGet** - Project with NuGet package references
   ```
   ├── NuGetSolution.sln
   └── NuGetProject/
       ├── NuGetProject.csproj (references Newtonsoft.Json)
       └── JsonUser.cs (uses JObject)
   ```

4. **SolutionWithAnalyzers** - Project with analyzer warnings
   ```
   ├── AnalyzerSolution.sln
   └── AnalyzerProject/
       ├── AnalyzerProject.csproj (includes StyleCop or similar)
       └── BadStyle.cs (IDE warnings)
   ```

### Critical Integration Tests

```csharp
[TestFixture]
public class WorkspaceLoadingTests
{
    [Test]
    public async Task ShouldLoadSimpleSolution()
    {
        var fixturePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures/SimpleSolution");
        var (success, _, projectCount) = await _workspace.InitializeAsync(fixturePath);

        success.Should().BeTrue();
        projectCount.Should().Be(1);
    }

    [Test]
    public async Task ShouldLoadSolutionWithMultipleProjects()
    {
        // Test multi-project solution loading
    }

    [Test]
    public async Task ShouldLoadCsprojWhenNoSlnPresent()
    {
        // Test .csproj-only loading
    }
}

[TestFixture]
public class DiagnosticsIntegrationTests
{
    [Test]
    public async Task ShouldReturnCompilerErrors()
    {
        // Load SolutionWithErrors
        // Get diagnostics
        // Verify CS0103 error is present with correct file/line
    }

    [Test]
    public async Task ShouldFilterDiagnosticsByFile()
    {
        // Load solution, get diagnostics for specific file only
    }

    [Test]
    public async Task ShouldFilterDiagnosticsByLineRange()
    {
        // Load solution, get diagnostics for lines 10-20 only
    }
}

[TestFixture]
public class SymbolResolutionTests
{
    [Test]
    public async Task ShouldResolveSymbolInWorkspaceCode()
    {
        // Load solution, find symbol at specific location
    }

    [Test]
    public async Task ShouldResolveSymbolInNuGetPackage()
    {
        // Load SolutionWithNuGet, find JObject symbol
    }

    [Test]
    public async Task ShouldDecompileTypeFromDll()
    {
        // Resolve System.String, decompile it
    }
}
```

---

## Solution Discovery Logic

When `initialize_workspace` is called with a directory:
1. Look for `.sln` files in the given directory
2. If multiple: prefer one with same name as directory, else pick largest (most projects)
3. If none: look for `.csproj` files, load as standalone project
4. If none: error

---

## Analyzer Support

Roslyn automatically loads analyzers referenced by the project. No special handling needed. The diagnostics will include analyzer warnings (IDE*, CA*, etc.) automatically.

---

## File Watching (defer to later)

For now, tools operate on the workspace snapshot from initialization. File watching for live updates can be added later. LLM can call `initialize_workspace` again if needed.

---

## Reference Documents

- Full specification: `csharp-lsp-mcp-spec.md` (in this repo)
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- Roslyn docs: https://github.com/dotnet/roslyn
- ICSharpCode.Decompiler: https://github.com/icsharpcode/ILSpy

---

## Definition of Done

### Phase 1 (Current)
- ✅ Server starts via `dotnet run`
- ✅ Can initialize workspace from .sln path
- ✅ Can initialize workspace from .csproj path
- ✅ Can initialize workspace from directory (auto-discover)
- ✅ Can get diagnostics for entire workspace
- ✅ Can get diagnostics filtered by file
- ✅ Can get diagnostics filtered by line range
- ✅ Can get diagnostics filtered by severity
- ✅ All unit tests pass
- ✅ All integration tests pass

### Phase 2 (Next)
- [ ] Can get symbol info at location
- [ ] Can get symbol info by fully qualified name
- [ ] Can find references to a symbol
- [ ] Can get definition location for workspace symbols
- [ ] Can get decompiled source for DLL types
- [ ] Can get full type definition with all members
- [ ] Integration tests cover all symbol operations

### Phase 3
- [ ] Can get available code actions at location
- [ ] Can apply code action with preview
- [ ] Can apply code action and modify files
- [ ] Integration tests cover refactoring scenarios

### Phase 4
- [ ] Can search symbols by name pattern
- [ ] Can get document symbol outline
- [ ] Integration tests cover search operations

### Final
- [ ] Works when configured in Claude Code via `.mcp.json`
- [ ] Comprehensive integration test suite covers common workflows
- [ ] No surprises when using in Cursor

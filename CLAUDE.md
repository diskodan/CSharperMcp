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

4. **`find_symbol_usages`** - All usages of a symbol
   - Input: `{ "file", "line", "column" }` OR `{ "symbolName" }`
   - Use `SymbolFinder.FindReferencesAsync()`
   - Return: array of `{ file, line, column, contextSnippet, referenceKind }`

5. **`get_definition_location`** - Navigate to definition location
   - Input: `{ "file", "line", "column" }` OR `{ "symbolName" }`
   - If in workspace: return file location
   - If in DLL: return metadata (assembly, type name, kind, signature)
   - Return: `{ file, line, column }` OR `{ assembly, typeName, symbolKind, signature, package }`

6. **`get_type_members`** - Full type definition with all members
   - Input: `{ "typeName": "System.String", "includeImplementation?": true }`
   - For workspace types: use Roslyn
   - For DLL types: use Decompiler
   - Return: full decompiled source or structured member list

### Phase 3: Code Actions

7. **`get_code_actions`** - Available refactorings/fixes at location
   - Input: `{ "file", "line", "column", "endLine?", "endColumn?", "diagnosticIds?": ["CS0001"] }`
   - **CRITICAL**: Use MEF-based dynamic discovery instead of hardcoded diagnostic list
   - Use MEF (Managed Extensibility Framework) composition to discover all available:
     - `CodeFixProvider` instances from Roslyn and analyzer packages
     - `CodeRefactoringProvider` instances
   - This ensures all analyzers/refactorings are automatically discovered
   - Return: array of `{ id, title, kind }`

8. **`apply_code_action`** - Execute a code action
   - Input: `{ "actionId", "file", "preview?": true }`
   - Apply the code action identified by actionId (from previous `get_code_actions` call)
   - If preview: return diff without modifying files
   - If not preview: apply changes to workspace, persist to disk, return modified files
   - Return: `{ changes: [{ file, diff }] }` or `{ applied: true, modifiedFiles: [] }`
   - Must handle multi-file changes (refactorings can span multiple files)

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

## Usage Examples

This section demonstrates real-world workflows using the MCP server tools. These examples show how tools work together to accomplish common development tasks.

### Example 1: Basic Workflow - Initialize, Get Diagnostics, Fix Errors

**Scenario**: You've cloned a new C# project and want to understand what's broken.

```json
// Step 1: Initialize the workspace
{
  "tool": "initialize_workspace",
  "arguments": {
    "path": "/home/user/projects/MyApp"
  }
}

// Response:
{
  "success": true,
  "projectCount": 3,
  "solutionPath": "/home/user/projects/MyApp/MyApp.sln",
  "projects": [
    "MyApp.Core",
    "MyApp.Web",
    "MyApp.Tests"
  ]
}

// Step 2: Get all diagnostics to see what's broken
{
  "tool": "get_diagnostics",
  "arguments": {}
}

// Response:
{
  "diagnostics": [
    {
      "id": "CS0103",
      "severity": "Error",
      "message": "The name 'logger' does not exist in the current context",
      "file": "/home/user/projects/MyApp/MyApp.Core/UserService.cs",
      "line": 42,
      "column": 13,
      "endLine": 42,
      "endColumn": 19,
      "hasFix": true
    },
    {
      "id": "CS0246",
      "severity": "Error",
      "message": "The type or namespace name 'JsonSerializer' could not be found",
      "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/ApiController.cs",
      "line": 15,
      "column": 21,
      "endLine": 15,
      "endColumn": 35,
      "hasFix": false
    }
  ]
}

// Step 3: Get diagnostics for a specific file to focus on one issue
{
  "tool": "get_diagnostics",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/UserService.cs"
  }
}

// Response shows only diagnostics in UserService.cs
// Step 4: Fix the issues, then re-run diagnostics to verify
```

**Key Takeaway**: Start broad (whole workspace), then narrow down (specific files) to systematically fix issues.

---

### Example 2: Symbol Exploration - Get Info, Navigate, Find Usages

**Scenario**: You found a class `OrderProcessor` and want to understand how it's used across the codebase.

```json
// Step 1: Get symbol info at cursor position
{
  "tool": "get_symbol_info",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
    "line": 10,
    "column": 18
  }
}

// Response:
{
  "kind": "Class",
  "name": "OrderProcessor",
  "namespace": "MyApp.Core.Processing",
  "assembly": "MyApp.Core",
  "containingType": null,
  "modifiers": ["public"],
  "signature": "public class OrderProcessor",
  "docComment": "Handles order processing and validation logic.",
  "locations": [
    {
      "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
      "line": 10,
      "column": 18
    }
  ]
}

// Step 2: Find all usages of this class
{
  "tool": "find_symbol_usages",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
    "line": 10,
    "column": 18
  }
}

// Response:
{
  "references": [
    {
      "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/OrderController.cs",
      "line": 23,
      "column": 20,
      "contextSnippet": "    private readonly OrderProcessor _processor;",
      "referenceKind": "Declaration"
    },
    {
      "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/OrderController.cs",
      "line": 30,
      "column": 42,
      "contextSnippet": "        var result = await _processor.ProcessOrder(order);",
      "referenceKind": "Read"
    },
    {
      "file": "/home/user/projects/MyApp/MyApp.Tests/OrderProcessorTests.cs",
      "line": 15,
      "column": 20,
      "contextSnippet": "        var processor = new OrderProcessor(mockRepo.Object);",
      "referenceKind": "ObjectCreation"
    }
  ]
}

// Step 3: Navigate to a specific usage
{
  "tool": "get_definition_location",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/OrderController.cs",
    "line": 30,
    "column": 52
  }
}

// Response:
{
  "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
  "line": 45,
  "column": 29,
  "symbolKind": "Method",
  "signature": "public async Task<OrderResult> ProcessOrder(Order order)"
}
```

**Key Takeaway**: Symbol tools work together - get info to understand what something is, find usages to see where it's used, get definition to jump to implementation.

---

### Example 3: DLL Introspection - Understand NuGet Package Code

**Scenario**: You're using `Newtonsoft.Json` and want to understand how `JObject.Parse()` works.

```json
// Step 1: Get symbol info for JObject.Parse (cursor on method call)
{
  "tool": "get_symbol_info",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Services/JsonService.cs",
    "line": 18,
    "column": 28
  }
}

// Response:
{
  "kind": "Method",
  "name": "Parse",
  "namespace": "Newtonsoft.Json.Linq",
  "assembly": "Newtonsoft.Json, Version=13.0.0.0",
  "containingType": "JObject",
  "modifiers": ["public", "static"],
  "signature": "public static JObject Parse(string json)",
  "docComment": "Load a JObject from a string that contains JSON.",
  "package": "Newtonsoft.Json 13.0.3",
  "isFromMetadata": true
}

// Step 2: Get definition location (will return metadata info, not file)
{
  "tool": "get_definition_location",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Services/JsonService.cs",
    "line": 18,
    "column": 28
  }
}

// Response:
{
  "assembly": "Newtonsoft.Json, Version=13.0.0.0",
  "typeName": "Newtonsoft.Json.Linq.JObject",
  "symbolKind": "Method",
  "signature": "public static JObject Parse(string json)",
  "package": "Newtonsoft.Json 13.0.3"
}

// Step 3: Get the entire JObject type with decompiled source
{
  "tool": "get_type_members",
  "arguments": {
    "typeName": "Newtonsoft.Json.Linq.JObject",
    "includeImplementation": true
  }
}

// Response:
{
  "typeName": "Newtonsoft.Json.Linq.JObject",
  "namespace": "Newtonsoft.Json.Linq",
  "assembly": "Newtonsoft.Json",
  "kind": "Class",
  "source": "// Decompiled with ICSharpCode.Decompiler\nnamespace Newtonsoft.Json.Linq\n{\n    public class JObject : JContainer, IDictionary<string, JToken>, ...\n    {\n        public static JObject Parse(string json)\n        {\n            using JsonReader reader = new JsonTextReader(new StringReader(json));\n            JObject result = Load(reader);\n            // ... full decompiled method body\n        }\n        // ... all other members\n    }\n}"
}
```

**Key Takeaway**: The server can introspect DLL code just like workspace code. Get symbol info to confirm you're looking at a NuGet type, then use `get_type_members` to see the full decompiled source.

---

### Example 4: Type Exploration - Workspace and DLL Types

**Scenario**: Compare your custom `Result<T>` type with the standard `Task<T>` to understand patterns.

```json
// Step 1: Get members of your custom type (workspace code)
{
  "tool": "get_type_members",
  "arguments": {
    "typeName": "MyApp.Core.Result",
    "includeImplementation": true
  }
}

// Response:
{
  "typeName": "MyApp.Core.Result",
  "namespace": "MyApp.Core",
  "assembly": "MyApp.Core",
  "kind": "Class",
  "source": "// From workspace source file\nnamespace MyApp.Core\n{\n    public class Result<T>\n    {\n        public bool IsSuccess { get; }\n        public T Value { get; }\n        public string Error { get; }\n        \n        public static Result<T> Success(T value) => new Result<T>(value);\n        public static Result<T> Failure(string error) => new Result<T>(error);\n        // ... rest of source\n    }\n}",
  "isFromMetadata": false
}

// Step 2: Get members of Task<T> from System DLLs (decompiled)
{
  "tool": "get_type_members",
  "arguments": {
    "typeName": "System.Threading.Tasks.Task",
    "includeImplementation": true
  }
}

// Response:
{
  "typeName": "System.Threading.Tasks.Task",
  "namespace": "System.Threading.Tasks",
  "assembly": "System.Private.CoreLib",
  "kind": "Class",
  "source": "// Decompiled with ICSharpCode.Decompiler\nnamespace System.Threading.Tasks\n{\n    public class Task<TResult> : Task\n    {\n        public TResult Result { get; }\n        public TaskAwaiter<TResult> GetAwaiter() { ... }\n        // ... full decompiled source\n    }\n}",
  "isFromMetadata": true
}

// Step 3: Get signatures only for large types (more token-efficient)
{
  "tool": "get_type_members",
  "arguments": {
    "typeName": "System.Threading.Tasks.Task",
    "includeImplementation": false
  }
}

// Response includes just method signatures without implementations
```

**Key Takeaway**: `get_type_members` works identically for workspace code and DLL code. Use `includeImplementation: false` for signatures only (more token-efficient for large types).

---

### Example 5: Refactoring Workflow - Get Actions, Preview, Apply

**Scenario**: You have a long method that needs to be refactored. Find available refactorings, preview changes, then apply.

```json
// Step 1: Get code actions available at the method
{
  "tool": "get_code_actions",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
    "line": 45,
    "column": 20,
    "endLine": 75,
    "endColumn": 5
  }
}

// Response:
{
  "actions": [
    {
      "id": "ExtractMethod_1",
      "title": "Extract Method",
      "kind": "Refactoring"
    },
    {
      "id": "InlineTemporary_2",
      "title": "Inline 'tempResult' variable",
      "kind": "Refactoring"
    },
    {
      "id": "ConvertToExpressionBody_3",
      "title": "Use expression body for method",
      "kind": "Refactoring"
    }
  ]
}

// Step 2: Preview the extract method refactoring
{
  "tool": "apply_code_action",
  "arguments": {
    "actionId": "ExtractMethod_1",
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
    "preview": true
  }
}

// Response:
{
  "changes": [
    {
      "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
      "diff": "@@ -45,30 +45,10 @@\n public async Task<OrderResult> ProcessOrder(Order order)\n {\n-    // Validate order\n-    if (order == null)\n-        throw new ArgumentNullException(nameof(order));\n-    if (order.Items.Count == 0)\n-        throw new InvalidOperationException(\"Order must have items\");\n-    // ... 20 more lines\n+    ValidateOrder(order);\n     var result = await _repository.SaveOrder(order);\n     return result;\n }\n+\n+private void ValidateOrder(Order order)\n+{\n+    if (order == null)\n+        throw new ArgumentNullException(nameof(order));\n+    if (order.Items.Count == 0)\n+        throw new InvalidOperationException(\"Order must have items\");\n+    // ... validation logic extracted here\n+}"
    }
  ]
}

// Step 3: Looks good! Apply it for real
{
  "tool": "apply_code_action",
  "arguments": {
    "actionId": "ExtractMethod_1",
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
    "preview": false
  }
}

// Response:
{
  "applied": true,
  "modifiedFiles": [
    "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs"
  ]
}

// Step 4: Verify no new errors were introduced
{
  "tool": "get_diagnostics",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs"
  }
}

// Response: diagnostics array is empty - success!
```

**Key Takeaway**: Always preview refactorings before applying. Code actions can span multiple files (e.g., rename, move type), so check the `modifiedFiles` list after applying.

---

### Example 6: Fixing Compiler Errors with Code Actions

**Scenario**: You have a compiler error and want to see if there's an automatic fix.

```json
// Step 1: Get diagnostics and find an error with hasFix=true
{
  "tool": "get_diagnostics",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/UserController.cs"
  }
}

// Response:
{
  "diagnostics": [
    {
      "id": "CS0246",
      "severity": "Error",
      "message": "The type or namespace name 'UserService' could not be found (are you missing a using directive or an assembly reference?)",
      "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/UserController.cs",
      "line": 15,
      "column": 20,
      "endLine": 15,
      "endColumn": 31,
      "hasFix": true
    }
  ]
}

// Step 2: Get code actions for this specific error
{
  "tool": "get_code_actions",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/UserController.cs",
    "line": 15,
    "column": 20,
    "diagnosticIds": ["CS0246"]
  }
}

// Response:
{
  "actions": [
    {
      "id": "AddUsing_MyApp.Core.Services",
      "title": "using MyApp.Core.Services;",
      "kind": "QuickFix"
    },
    {
      "id": "FullyQualify_MyApp.Core.Services.UserService",
      "title": "Fully qualify 'UserService'",
      "kind": "QuickFix"
    }
  ]
}

// Step 3: Apply the "add using" fix
{
  "tool": "apply_code_action",
  "arguments": {
    "actionId": "AddUsing_MyApp.Core.Services",
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/UserController.cs",
    "preview": false
  }
}

// Response:
{
  "applied": true,
  "modifiedFiles": [
    "/home/user/projects/MyApp/MyApp.Web/Controllers/UserController.cs"
  ]
}

// Step 4: Verify the error is gone
{
  "tool": "get_diagnostics",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/UserController.cs"
  }
}

// Response: diagnostics array is empty or no longer contains CS0246
```

**Key Takeaway**: Many compiler errors have automatic fixes. Use `hasFix: true` from diagnostics to identify fixable issues, then get and apply the appropriate code action.

---

### Example 7: Cross-Project Symbol Navigation

**Scenario**: You're in a web controller calling a method from your Core library. You want to see the implementation.

```json
// Step 1: Get symbol info for the method call
{
  "tool": "get_symbol_info",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/OrderController.cs",
    "line": 32,
    "column": 45
  }
}

// Response:
{
  "kind": "Method",
  "name": "ProcessOrder",
  "namespace": "MyApp.Core.Processing",
  "assembly": "MyApp.Core",
  "containingType": "OrderProcessor",
  "modifiers": ["public", "async"],
  "signature": "public async Task<OrderResult> ProcessOrder(Order order)",
  "docComment": "Processes the given order and returns the result.",
  "isFromMetadata": false
}

// Step 2: Navigate to the definition
{
  "tool": "get_definition_location",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Web/Controllers/OrderController.cs",
    "line": 32,
    "column": 45
  }
}

// Response:
{
  "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
  "line": 45,
  "column": 29,
  "symbolKind": "Method",
  "signature": "public async Task<OrderResult> ProcessOrder(Order order)"
}

// Step 3: Read the implementation at that location
// (LLM would use file reading tools here)

// Step 4: Find all other places this method is called
{
  "tool": "find_symbol_usages",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/OrderProcessor.cs",
    "line": 45,
    "column": 29
  }
}

// Response shows all calls across all projects in solution
```

**Key Takeaway**: The server seamlessly navigates across project boundaries. Symbol info, definition location, and find usages all work across the entire solution.

---

### Example 8: Understanding Extension Methods

**Scenario**: You see `.ToDictionary()` called on a collection and want to understand where it comes from and what other LINQ methods are available.

```json
// Step 1: Get symbol info for the extension method call
{
  "tool": "get_symbol_info",
  "arguments": {
    "file": "/home/user/projects/MyApp/MyApp.Core/DataProcessor.cs",
    "line": 28,
    "column": 35
  }
}

// Response:
{
  "kind": "Method",
  "name": "ToDictionary",
  "namespace": "System.Linq",
  "assembly": "System.Linq",
  "containingType": "Enumerable",
  "modifiers": ["public", "static"],
  "signature": "public static Dictionary<TKey, TElement> ToDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)",
  "docComment": "Creates a Dictionary<TKey,TValue> from an IEnumerable<T> according to specified key selector and element selector functions.",
  "isExtensionMethod": true,
  "isFromMetadata": true
}

// Step 2: Get all extension methods available for IEnumerable<T>
{
  "tool": "get_extension_methods",
  "arguments": {
    "typeName": "System.Collections.Generic.IEnumerable<T>",
    "includeDocumentation": true
  }
}

// Response:
{
  "typeName": "System.Collections.Generic.IEnumerable<T>",
  "extensionMethods": [
    {
      "name": "Select",
      "signature": "public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)",
      "containingType": "System.Linq.Enumerable",
      "assembly": "System.Linq",
      "docComment": "Projects each element of a sequence into a new form."
    },
    {
      "name": "Where",
      "signature": "public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)",
      "containingType": "System.Linq.Enumerable",
      "assembly": "System.Linq",
      "docComment": "Filters a sequence of values based on a predicate."
    },
    {
      "name": "ToDictionary",
      "signature": "public static Dictionary<TKey, TElement> ToDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)",
      "containingType": "System.Linq.Enumerable",
      "assembly": "System.Linq",
      "docComment": "Creates a Dictionary<TKey,TValue> from an IEnumerable<T>..."
    }
    // ... all other LINQ extension methods
  ]
}
```

**Key Takeaway**: Extension methods are first-class citizens. `get_symbol_info` identifies them, and `get_extension_methods` discovers all available extensions for a type - incredibly useful for API discovery.

---

## Best Practices for Using MCP Tools

### 1. Start Broad, Then Narrow
- Get workspace-wide diagnostics first, then filter by file or line range
- Search for symbols across the solution, then focus on specific usages

### 2. Always Preview Refactorings
- Use `preview: true` before applying code actions that modify files
- Check the diff to understand what will change
- Multi-file refactorings (rename, move type) need extra caution

### 3. Combine Tools for Complete Understanding
- `get_symbol_info` → understand what something is
- `get_definition_location` → see where it's defined
- `find_symbol_usages` → see where it's used
- `get_type_members` → see the full API surface

### 4. Verify After Changes
- Run `get_diagnostics` after applying code actions to catch new errors
- Re-initialize workspace if major changes (project file edits, package additions)

### 5. Leverage DLL Introspection
- Don't grep NuGet package source - use `get_type_members` to decompile
- Use `get_extension_methods` to discover APIs without reading docs
- Symbol info always tells you if something is from metadata (`isFromMetadata: true`)

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

## Configuration File Support

YAML configuration files allow customization of MCP server behavior and tool descriptions without code changes:

- **File locations** (hierarchical merging, later files override earlier):
  1. `~/.config/csharp-er-mcp.yml` - User global preferences (optional)
  2. `<workspace>/.config/csharp-er-mcp.yml` - Project-specific config (optional)

- **Format**:
  ```yaml
  # Server configuration (optional - has defaults)
  mcp:
    serverInstructions: "Custom instructions for the MCP server..."

  # Tool descriptions (optional)
  tools:
    initialize_workspace:
      description: "Custom description here..."
      isEnabled: true
    get_diagnostics:
      description: "Custom description here..."
      isEnabled: true
    some_tool_to_hide:
      isEnabled: false  # Tool will be filtered out
  ```

- **Features**:
  - Override server instructions sent to clients
  - Customize individual tool descriptions
  - Disable tools via `isEnabled: false`
  - Configuration merging: user config → project config → command-line overrides

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

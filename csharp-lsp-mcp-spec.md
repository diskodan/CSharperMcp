# C# LSP MCP Server Project Specification

## Project Overview

**Project Name:** CSharpContext (working title)

**Goal:** Create a local MCP (Model Context Protocol) server that provides LLMs with rich, IDE-like semantic understanding of C# code - the same level of intelligence that JetBrains Rider or VS Code with C# extension provides.

**Problem Statement:** Current AI coding tools (Claude Code, Cursor, etc.) lack native Language Server Protocol (LSP) integration for C#. The recently released C# LSP options are buggy and incomplete. Without LSP integration, LLMs must resort to grepping files and brute-force reverse engineering, which is slow, error-prone, and misses critical semantic context (especially for code in third-party DLLs/NuGet packages).

**Target Consumers:** 
- Claude Code (via MCP)
- Cursor (via MCP)
- Any MCP-compatible LLM tool

---

## Architecture Decision: MCP Server

### Why MCP over Claude Code Plugin?

After researching the current landscape:

1. **MCP is the standard** - It works across Claude Code, Cursor, and many other tools. Plugins are Claude Code-specific.
2. **Plugins can include MCP servers** - Claude Code plugins are actually bundles that can contain MCP servers, slash commands, hooks, and skills. You'd still build the MCP server.
3. **Portability** - MCP servers can be used standalone or packaged into plugins later.
4. **MCP is not falling out of favor** - It's becoming more integrated. Skills complement MCP but don't replace it. Skills teach Claude *when/how* to use tools; MCP provides the tools themselves.

**Recommendation:** Build as an MCP server first. Optionally package as a Claude Code plugin later for better distribution.

### Technology Stack

**Language:** C# (.NET 8+)

**Rationale:**
- Native Roslyn integration (no marshalling overhead)
- First-class access to Microsoft.CodeAnalysis APIs
- ICSharpCode.Decompiler for DLL introspection
- Strong NuGet ecosystem
- Official MCP C# SDK available: [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)

**Key Dependencies:**
```xml
<PackageReference Include="ModelContextProtocol" Version="*" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="*" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="*" />
<PackageReference Include="ICSharpCode.Decompiler" Version="*" />
<PackageReference Include="Microsoft.Build.Locator" Version="*" />
```

---

## Core Features

### 1. Solution/Project Loading

**Initialization:**
- Consumer provides project root path at startup (via MCP initialization)
- Server discovers and loads `.sln` file (or `.csproj` if no solution)
- Builds Roslyn workspace with full semantic model
- Indexes all symbols for fast lookup

**MCP Tool: `initialize_workspace`**
```json
{
  "name": "initialize_workspace",
  "description": "Initialize the C# workspace with a solution or project path",
  "inputSchema": {
    "type": "object",
    "properties": {
      "path": {
        "type": "string",
        "description": "Absolute path to .sln or .csproj file, or directory containing them"
      }
    },
    "required": ["path"]
  }
}
```

---

### 2. Symbol Information

**Get type information for any symbol by file location or name.**

**MCP Tool: `get_symbol_info`**
```json
{
  "name": "get_symbol_info",
  "description": "Get detailed type information for a symbol at a specific location or by name",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file": { "type": "string", "description": "File path (relative to workspace root)" },
      "line": { "type": "integer", "description": "1-based line number" },
      "column": { "type": "integer", "description": "1-based column number" },
      "symbolName": { "type": "string", "description": "Alternative: fully qualified symbol name" }
    }
  }
}
```

**Response includes:**
- Symbol kind (class, method, property, field, etc.)
- Fully qualified name
- Containing type and namespace
- Type information (return type, parameter types)
- Assembly/package origin (including NuGet package name and version)
- XML documentation comments
- Modifiers (public, static, async, etc.)
- Generic type parameters and constraints

---

### 3. Find Usages / References

**Find all locations where a symbol is used.**

**MCP Tool: `find_references`**
```json
{
  "name": "find_references",
  "description": "Find all references to a symbol",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file": { "type": "string" },
      "line": { "type": "integer" },
      "column": { "type": "integer" },
      "symbolName": { "type": "string" },
      "includeDefinition": { "type": "boolean", "default": true }
    }
  }
}
```

**Response includes:**
- List of locations (file, line, column, context snippet)
- Reference kind (read, write, invocation, type reference, etc.)
- Grouped by file

---

### 4. Go to Definition / Type Definition

**Navigate to where a symbol is defined, including decompiled sources for DLL types.**

**MCP Tool: `get_definition`**
```json
{
  "name": "get_definition",
  "description": "Get the definition of a symbol. For DLL types, returns decompiled source.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file": { "type": "string" },
      "line": { "type": "integer" },
      "column": { "type": "integer" },
      "symbolName": { "type": "string" }
    }
  }
}
```

**Response includes:**
- Source location (file, line, column) OR decompiled source code
- For DLL types: full decompiled C# source from ICSharpCode.Decompiler
- Assembly and NuGet package information

**MCP Tool: `get_type_definition`**
```json
{
  "name": "get_type_definition",
  "description": "Get full type definition including all members, even from DLLs",
  "inputSchema": {
    "type": "object",
    "properties": {
      "typeName": { "type": "string", "description": "Fully qualified type name, e.g., 'System.String' or 'MyApp.Services.FooDto'" },
      "includeInherited": { "type": "boolean", "default": false },
      "includePrivate": { "type": "boolean", "default": false }
    }
  }
}
```

**Response includes:**
- Full decompiled source OR source file location
- All public members (methods, properties, fields, events)
- Base types and interfaces
- XML documentation

---

### 5. Diagnostics / Problems

**Get compiler errors, warnings, and analyzer messages at various scopes.**

**MCP Tool: `get_diagnostics`**
```json
{
  "name": "get_diagnostics",
  "description": "Get compiler errors, warnings, and analyzer diagnostics. Supports workspace-wide, file-level, or range-scoped queries.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file": { 
        "type": "string", 
        "description": "Optional: specific file path. If omitted, returns diagnostics for entire workspace." 
      },
      "startLine": { 
        "type": "integer", 
        "description": "Optional: filter to diagnostics starting at or after this line (1-based)" 
      },
      "endLine": { 
        "type": "integer", 
        "description": "Optional: filter to diagnostics ending at or before this line (1-based)" 
      },
      "severity": { 
        "type": "string", 
        "enum": ["error", "warning", "info", "hidden"],
        "description": "Minimum severity to include. Default: 'warning'"
      },
      "includeSuppressions": {
        "type": "boolean",
        "default": false,
        "description": "Include suppressed diagnostics"
      }
    }
  }
}
```

**Usage patterns:**
- `{}` - All diagnostics across workspace (errors + warnings)
- `{"file": "Services/Foo.cs"}` - All diagnostics in one file
- `{"file": "Services/Foo.cs", "startLine": 50, "endLine": 100}` - Diagnostics in a specific range (useful when LLM is editing a section)
- `{"severity": "error"}` - Only errors, workspace-wide

**Response includes:**
- Diagnostic ID (e.g., CS0117, IDE0001)
- Message
- Severity
- Location (file, line, column, endLine, endColumn)
- Category (compiler, analyzer name)
- Suggested fixes available (boolean - use `get_code_actions` to retrieve them)

**Critical for LLM workflow:** LLM can check for errors immediately after making edits, before attempting to run tests. The range filter lets it focus on just the area it modified.

---

### 6. Code Actions / Quick Fixes

**Get available refactorings and fixes, and apply them.**

**MCP Tool: `get_code_actions`**
```json
{
  "name": "get_code_actions",
  "description": "Get available code actions (refactorings, quick fixes) at a location",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file": { "type": "string" },
      "line": { "type": "integer" },
      "column": { "type": "integer" },
      "endLine": { "type": "integer", "description": "For range selection" },
      "endColumn": { "type": "integer" },
      "diagnosticIds": { 
        "type": "array", 
        "items": { "type": "string" },
        "description": "Filter to fixes for specific diagnostics"
      }
    }
  }
}
```

**MCP Tool: `apply_code_action`**
```json
{
  "name": "apply_code_action",
  "description": "Apply a code action by its ID",
  "inputSchema": {
    "type": "object",
    "properties": {
      "actionId": { "type": "string" },
      "file": { "type": "string" },
      "preview": { "type": "boolean", "default": true, "description": "Return diff instead of applying" }
    },
    "required": ["actionId", "file"]
  }
}
```

**Actions include:**
- Add using directive
- Rename symbol (all references)
- Extract method/variable
- Inline variable/method
- Remove unused using directives
- Implement interface
- Generate constructor/Equals/GetHashCode
- Convert to expression body
- And all other Roslyn code fixes and refactorings

---

### 7. Symbol Search

**Search for symbols by name across the workspace.**

**MCP Tool: `search_symbols`**
```json
{
  "name": "search_symbols",
  "description": "Search for symbols by name pattern",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": { "type": "string", "description": "Search pattern (supports camelCase matching)" },
      "kinds": { 
        "type": "array",
        "items": { "type": "string", "enum": ["class", "interface", "struct", "enum", "method", "property", "field", "event", "namespace"] }
      },
      "maxResults": { "type": "integer", "default": 50 }
    },
    "required": ["query"]
  }
}
```

---

### 8. Document Outline / Structure

**Get the structure of a file or type.**

**MCP Tool: `get_document_symbols`**
```json
{
  "name": "get_document_symbols",
  "description": "Get the hierarchical symbol structure of a document",
  "inputSchema": {
    "type": "object",
    "properties": {
      "file": { "type": "string" }
    },
    "required": ["file"]
  }
}
```

---

### 9. NuGet Package Management

**MCP Tool: `list_packages`**
```json
{
  "name": "list_packages",
  "description": "List NuGet packages referenced by a project",
  "inputSchema": {
    "type": "object",
    "properties": {
      "project": { "type": "string", "description": "Project name or path" }
    }
  }
}
```

**MCP Tool: `search_nuget`**
```json
{
  "name": "search_nuget",
  "description": "Search NuGet.org for packages",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": { "type": "string" },
      "take": { "type": "integer", "default": 10 }
    },
    "required": ["query"]
  }
}
```

**MCP Tool: `add_package`**
```json
{
  "name": "add_package",
  "description": "Add a NuGet package to a project",
  "inputSchema": {
    "type": "object",
    "properties": {
      "project": { "type": "string" },
      "packageId": { "type": "string" },
      "version": { "type": "string", "description": "Optional: specific version" }
    },
    "required": ["project", "packageId"]
  }
}
```

---

## Special Feature: Extension Method Discovery

### The Problem

You have a library of extension methods like:
```csharp
public static T? GetOrDefault<T,K>(this IDictionary<K,T> dict, K key) 
    => dict.TryGetValue(key, out var val) ? val : default;
```

The LLM doesn't know these exist (they're in a DLL) and even if it did, doesn't know when to use them.

### Solution: LLM-Accessible Documentation

**Option A: Enhanced XML Documentation**

Add comprehensive XML docs with examples directly in source:
```csharp
/// <summary>
/// Safely retrieves a value from a dictionary, returning default if key not found.
/// </summary>
/// <remarks>
/// ## When to use
/// Use this instead of the TryGetValue pattern when:
/// - You want a one-liner for dictionary lookups
/// - The default value (null/0/false) is acceptable for missing keys
/// - You don't need to distinguish "key not found" from "key found with default value"
/// 
/// ## Examples
/// ```csharp
/// // Instead of:
/// if (dict.TryGetValue("key", out var val)) { use(val); } else { use(null); }
/// 
/// // Write:
/// var val = dict.GetOrDefault("key");
/// ```
/// </remarks>
public static T? GetOrDefault<T,K>(this IDictionary<K,T> dict, K key) => ...
```

**Option B: Assembly-Level Attributes**

Create custom attributes for LLM context:
```csharp
[assembly: LlmDocumentation("DictionaryExtensions", @"
This class provides dictionary extension methods that simplify common patterns:
- GetOrDefault: Safe lookup with default fallback
- GetOrAdd: Lazy initialization pattern
- AddRange: Bulk addition from sequence
...
")]
```

**Option C: Companion Markdown Files**

Store `.llm.md` files alongside assemblies:
```
MyExtensions.dll
MyExtensions.llm.md  <- Server reads this for LLM context
```

**MCP Tool: `get_extension_methods`**
```json
{
  "name": "get_extension_methods",
  "description": "Find extension methods available for a type",
  "inputSchema": {
    "type": "object",
    "properties": {
      "typeName": { "type": "string", "description": "Type to find extensions for, e.g., 'IDictionary<,>'" },
      "includeDocumentation": { "type": "boolean", "default": true }
    },
    "required": ["typeName"]
  }
}
```

**MCP Resource: Extension Method Catalog**

Register extension method documentation as MCP resources:
```json
{
  "uri": "csharp://extensions/MyExtensions.DictionaryExtensions",
  "name": "Dictionary Extension Methods",
  "description": "Extension methods for IDictionary<K,V>",
  "mimeType": "text/markdown"
}
```

---

## Implementation Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        MCP Server                                │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                    Tool Handlers                            ││
│  │  get_symbol_info | find_references | get_definition | ...   ││
│  └─────────────────────────────────────────────────────────────┘│
│                              │                                   │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                 Workspace Manager                           ││
│  │  - Solution loading        - Document synchronization       ││
│  │  - Project management      - Incremental compilation        ││
│  └─────────────────────────────────────────────────────────────┘│
│                              │                                   │
│  ┌───────────────────┐ ┌───────────────────┐ ┌────────────────┐│
│  │  Roslyn Services  │ │   Decompiler     │ │ NuGet Client   ││
│  │  - SemanticModel  │ │   (ILSpy)        │ │                ││
│  │  - SymbolFinder   │ │   - Type lookup  │ │ - Search       ││
│  │  - CodeFixes      │ │   - Decompile    │ │ - Add package  ││
│  │  - Rename         │ │   - Metadata     │ │ - List refs    ││
│  └───────────────────┘ └───────────────────┘ └────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

---

## MCP Server Configuration

**Startup command (stdio transport):**
```bash
dotnet run --project CSharpContext.csproj -- --workspace /path/to/solution
```

**Claude Code MCP configuration (`.mcp.json`):**
```json
{
  "mcpServers": {
    "csharp-context": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/CSharpContext.csproj", "--", "--workspace", "${workspaceFolder}"],
      "env": {}
    }
  }
}
```

**Alternative: Published tool:**
```json
{
  "mcpServers": {
    "csharp-context": {
      "command": "csharp-context",
      "args": ["--workspace", "${workspaceFolder}"]
    }
  }
}
```

---

## Project Structure

```
CSharpContext/
├── CSharpContext.csproj
├── Program.cs                    # Entry point, MCP server setup
├── Server/
│   ├── CSharpMcpServer.cs       # MCP server implementation
│   └── Tools/
│       ├── SymbolInfoTool.cs
│       ├── FindReferencesTool.cs
│       ├── GetDefinitionTool.cs
│       ├── DiagnosticsTool.cs
│       ├── CodeActionsTool.cs
│       ├── SearchSymbolsTool.cs
│       └── NuGetTool.cs
├── Workspace/
│   ├── WorkspaceManager.cs       # Solution/project loading
│   ├── DocumentSync.cs           # Track file changes
│   └── SymbolIndex.cs            # Fast symbol lookup
├── Services/
│   ├── RoslynService.cs          # Roslyn API wrapper
│   ├── DecompilerService.cs      # ICSharpCode.Decompiler wrapper
│   ├── NuGetService.cs           # NuGet API client
│   └── ExtensionMethodRegistry.cs # Extension method discovery
└── Models/
    ├── SymbolInfo.cs
    ├── ReferenceInfo.cs
    └── DiagnosticInfo.cs
```

---

## Development Phases

### Phase 1: Core Foundation
1. MCP server skeleton with stdio transport
2. Workspace loading (solution/project discovery)
3. Basic symbol info by file location
4. Diagnostics retrieval

### Phase 2: Navigation
1. Find references
2. Go to definition (source files)
3. Go to definition (decompiled DLLs)
4. Type definition with full member listing
5. Symbol search

### Phase 3: Intelligence
1. Code actions / quick fixes
2. Apply code action with preview
3. Document symbols / outline

### Phase 4: Package Management
1. List packages
2. Search NuGet
3. Add/remove packages
4. Restore packages

### Phase 5: Extension Method Support
1. Extension method discovery
2. LLM documentation integration
3. Custom attribute support
4. Resource registration

### Phase 6: Polish
1. Incremental compilation for performance
2. File watching for external changes
3. Better error handling and recovery
4. Logging and diagnostics

---

## Testing Strategy

1. **Unit tests** for each service (Roslyn, Decompiler, NuGet)
2. **Integration tests** with sample C# projects
3. **MCP conformance tests** using official SDK test utilities
4. **Real-world testing** with actual Claude Code/Cursor usage

---

## Related Projects / Prior Art

- **[Serena MCP](https://github.com/oraios/serena)** - LSP-based MCP server supporting multiple languages including C#. Good reference but doesn't provide DLL introspection.
- **[csharp-language-server](https://github.com/razzmatazz/csharp-language-server)** - Roslyn-based LSP server. Uses ILSpy for decompilation. Good architecture reference.
- **[vscode-csharp](https://github.com/dotnet/vscode-csharp)** - Official C# VS Code extension. Uses Roslyn LSP server.
- **[OmniSharp-roslyn](https://github.com/OmniSharp/omnisharp-roslyn)** - Original Roslyn-based language server.
- **[MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)** - Official C# implementation of MCP.

---

## Success Criteria

1. **LLM can understand code semantically** - Get type info, find usages, navigate to definitions
2. **Works with third-party code** - Decompile and introspect NuGet packages and DLLs
3. **Catches errors early** - LLM knows about compiler errors before running code
4. **Enables automated refactoring** - Rename, extract, inline with confidence
5. **Discovers extension methods** - LLM learns about custom utilities and when to use them
6. **Works across tools** - Compatible with Claude Code, Cursor, and other MCP clients

---

## Design Decisions

1. **Incremental updates**: Start with full recompilation on changes; optimize to incremental later if perf is an issue. Roslyn handles this reasonably well.
2. **Multi-solution support**: One server instance handles one primary solution. If multiple `.sln` files exist, pick the one in the root or the largest by project count. User can override via initialization.
3. **Authentication**: None needed - purely local server.
4. **Caching**: Defer aggressive caching to later phases. Start simple, profile, then optimize.
5. **Analyzer support**: Load and run whatever analyzers the project already references (StyleCop, Roslynator, etc.). No special handling needed - Roslyn does this automatically when loading the workspace.

---

## Getting Started (For Implementation)

1. Create new C# console project targeting .NET 8+
2. Add MCP SDK: `dotnet add package ModelContextProtocol --prerelease`
3. Add Roslyn: `dotnet add package Microsoft.CodeAnalysis.Workspaces.MSBuild`
4. Add MSBuild locator: `dotnet add package Microsoft.Build.Locator`
5. Add ILSpy: `dotnet add package ICSharpCode.Decompiler`
6. Implement basic server with `initialize_workspace` and `get_diagnostics` tools
7. Test with Claude Code using `.mcp.json` configuration
8. Iterate on additional tools

---

*This specification is a living document. Update as implementation progresses and requirements evolve.*

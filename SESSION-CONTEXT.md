# Session Context - CSharperMcp Development

**Last Updated**: 2026-01-19
**Status**: Phase 3 complete - apply_code_action tool implemented
**Tests**: All 50 tests passing (7 unit + 43 integration)

## Recent Accomplishments ✅

### 1. MEF-Based Code Action Discovery (Completed)
- **Commit**: `4cff02d` - Add MEF-based code action discovery with smart filtering
- **What was done**:
  - Added System.Composition packages (v9.0.0) for MEF v2 support
  - Created `CodeActionProviderService` that dynamically discovers:
    - `CodeFixProvider` instances from Roslyn and analyzer packages
    - `CodeRefactoringProvider` instances
  - Uses OmniSharp's reflection-based discovery pattern
  - Replaced hardcoded diagnostic list with dynamic discovery
  - Providers are cached per-project for performance

### 2. Smart Code Action Filtering (Completed)
- **Created**: `CodeActionFilterConfiguration.cs`
- **Purpose**: Reduces noise and improves LLM usability by filtering out low-value suggestions
- **Features**:
  - **Excluded by default**: Noisy style suggestions (IDE0001, IDE0002, IDE0004, IDE0055, IDE0071, IDE0072, IDE0082)
  - **Excluded refactorings**: "Sort usings", "Organize usings", "Add file banner", "Generate XML documentation comment"
  - **Priority diagnostics** (always shown): CS0103, CS0246, CS1061, CS0029, CS0168, CS0219, CA1031, CA2007
  - **Deduplication**: Removes duplicate actions with identical titles
  - **Max results**: Default 50 (configurable)
  - **Toggle refactorings**: Can disable all refactorings if needed

## Current Architecture

### Key Services
- **WorkspaceManager** (`Workspace/WorkspaceManager.cs`) - Loads .sln/.csproj files, manages Roslyn workspace
- **RoslynService** (`Services/RoslynService.cs`) - Wraps Roslyn APIs for diagnostics, symbol lookup
- **DecompilerService** (`Services/DecompilerService.cs`) - Wraps ICSharpCode.Decompiler for DLL introspection
- **CodeActionProviderService** (`Services/CodeActionProviderService.cs`) - NEW: Discovers code fix/refactoring providers
- **CodeActionsService** (`Services/CodeActionsService.cs`) - UPDATED: Uses MEF discovery + filtering

### MCP Tools Implemented
1. ✅ `initialize_workspace` - Load solution/project from path
2. ✅ `get_diagnostics` - Compiler errors/warnings/analyzer messages
3. ✅ `get_code_actions` - Available refactorings/fixes at location (MEF-based)
4. ✅ `apply_code_action` - Execute code actions with preview/apply modes

### Configuration
- **WorkspaceConfiguration** - `--workspace` parameter support
- **CodeActionFilterConfiguration** - NEW: Smart filtering for code actions
- Both use `IOptions<T>` pattern, registered in `Program.cs`

### 3. Apply Code Action Tool (Completed)
- **Commit**: Pending
- **What was done**:
  - Created `ApplyCodeActionResult` and `FileChange` models
  - Added `ConcurrentDictionary<string, CodeAction>` cache in `CodeActionsService`
  - Implemented `ApplyCodeActionAsync(actionId, preview)` method
  - **Preview mode**: Returns unified diffs without modifying files
  - **Apply mode**: Persists changes to disk using `Workspace.TryApplyChanges()`
  - Handles multi-file changes (added, modified, and removed documents)
  - Added `UpdateCurrentSolution` method to `WorkspaceManager`
  - Exposed `Workspace` property in `WorkspaceManager`
  - Created `ApplyCodeActionTool` MCP tool
  - Added 5 unit tests for model validation
  - Added 4 integration tests for preview/apply modes
  - All 50 tests passing

**Key Implementation Details**:
- Cache strategy: Session-based (no expiration, cleared on restart)
- Action ID format: `fix_{EquivalenceKey ?? Guid.NewGuid()}` or `refactor_{...}`
- Diff format: Unified diff format with context lines
- Multi-file handling: Returns array of `FileChange` objects, one per file
- Error handling: Returns `ApplyCodeActionResult` with success flag and error message

**Answers to Design Questions**:
1. **Cache strategy**: Session-based (Option A) - simplest and sufficient for current use case
2. **Action ID**: Uses `EquivalenceKey` when available, falls back to GUID
3. **Diff format**: Unified diff format (industry standard)
4. **Multi-file**: Returns all changes in single response as array

## Remaining Work

### Priority 1: Add Configuration File Support (Medium Priority)

**Goal**: Allow users to customize MCP tool descriptions without modifying code

**Requirements** (from CLAUDE.md):
- File location: `..csharper.yaml` or `csharp-er-mcp.yaml` in workspace root
- Purpose: Tailor tool descriptions for different LLM contexts
- Format:
  ```yaml
  tools:
    initialize_workspace:
      description: "Custom description here..."
    get_diagnostics:
      description: "Custom description here..."
      parameters:
        file:
          description: "Custom parameter description..."
  ```
- Load at server startup, fall back to default descriptions if not present
- Consider adding support for per-tool examples

**Implementation Notes**:
- Use a YAML parsing library (e.g., YamlDotNet)
- Load during DI configuration in `Program.cs`
- Create a middleware/decorator to override tool metadata
- May need to intercept MCP tool registration

### Priority 2: Pre-1.0 Usability Reviews (Medium Priority)

**From pending-todos.md**:

1. **Tool names review** - Ensure consistency (snake_case everywhere)
2. **Parameter names review** - Clarity and LLM-friendliness
3. **Return value structure review** - Consistency across tools, PascalCase for properties

## Phase 2 Remaining Work (Symbol Intelligence)

Not yet started. From CLAUDE.md:

3. **`get_symbol_info`** - Type info at location or by name
4. **`find_references`** - All usages of a symbol
5. **`get_definition`** - Go to definition (source or decompiled)
6. **`get_type_members`** - Full type definition with all members

## Phase 4 Work (Search & Navigation)

Not yet started. From CLAUDE.md:

9. **`search_symbols`** - Find symbols by name pattern (with camelCase matching)
10. **`get_document_symbols`** - File outline/structure

## Phase 5 Work (Extension Methods)

Not yet started. From CLAUDE.md:

11. **`get_extension_methods`** - Find extensions for a type (special feature)

## Important Patterns & Conventions

### Code Style
- Default to `internal` for all types (tests have `InternalsVisibleTo`)
- Use latest C# features (records, pattern matching, file-scoped namespaces, primary constructors)
- Nullable reference types enabled - respect annotations
- Use extension methods for null-safe operations (`.OrEmpty()`, `.IsNullOrEmpty()`)

### Testing
- **NUnit 4** with `FixtureLifeCycle.InstancePerTestCase`
- Unit tests: Fast, isolated, mock all dependencies
- Integration tests: Test real Roslyn workspace loading, real projects
- Integration tests are the primary defense against regressions
- Test fixtures in `tests/CSharperMcp.Server.IntegrationTests/Fixtures/`

### Package Management
- **Central Package Management (CPM)** - All versions in `Directory.Packages.props`
- ✅ Correct: `<PackageReference Include="Moq" />`
- ❌ Wrong: `<PackageReference Include="Moq" Version="4.20.72" />`

### Critical Runtime Requirement
- **MUST** call `MSBuildLocator.RegisterDefaults()` before any Roslyn types are loaded
- This is done in `Program.cs` at the very top

## Key Files to Understand

1. **CLAUDE.md** - Full project specification and guidelines
2. **pending-todos.md** - Current task list with status
3. **csharp-lsp-mcp-spec.md** - Original specification (if exists in repo)
4. **Program.cs** - Entry point, DI configuration, MSBuild locator setup
5. **CodeActionsService.cs** - Recently updated with MEF discovery
6. **CodeActionProviderService.cs** - NEW: Provider discovery service
7. **CodeActionFilterConfiguration.cs** - NEW: Filtering configuration

## References

- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- Roslyn docs: https://github.com/dotnet/roslyn
- ICSharpCode.Decompiler: https://github.com/icsharpcode/ILSpy
- OmniSharp (pattern reference): https://github.com/OmniSharp/omnisharp-roslyn

## Development Commands

```bash
# Build everything
dotnet build

# Run all tests
dotnet test

# Run only unit tests (fast)
dotnet test tests/CSharperMcp.Server.UnitTests

# Run only integration tests
dotnet test tests/CSharperMcp.Server.IntegrationTests

# Run the server with auto-workspace initialization
dotnet run --project src/CSharperMcp.Server -- --workspace /path/to/solution
```

## Next Session Starter

When resuming work, recommend:
1. Read this document
2. Review `pending-todos.md` for current task list
3. Consider adding configuration file support (next on priority list)
4. Or start implementing Phase 2 symbol intelligence tools
5. Run all tests before committing

## Questions to Resolve

None currently - all Phase 3 design questions have been answered in the implementation.

## Git Status

- Working tree dirty (apply_code_action implementation not yet committed)
- Latest commit: `4cff02d` - Add MEF-based code action discovery with smart filtering
- Branch: `main`
- Ready to commit apply_code_action implementation

# Session Context - CSharperMcp Development

**Last Updated**: 2026-01-18
**Status**: Phase 3 in progress - MEF-based code action discovery complete
**Tests**: All 43 tests passing (2 unit + 41 integration)

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

### Configuration
- **WorkspaceConfiguration** - `--workspace` parameter support
- **CodeActionFilterConfiguration** - NEW: Smart filtering for code actions
- Both use `IOptions<T>` pattern, registered in `Program.cs`

## Remaining Work

### Priority 1: Implement apply_code_action Tool (Phase 3)

**Goal**: Execute code actions discovered by `get_code_actions`

**Requirements** (from CLAUDE.md Phase 3):
- Input: `{ "actionId", "file", "preview?": true }`
- If preview: return diff without modifying files
- If not preview: apply changes to workspace, persist to disk
- Return: `{ changes: [{ file, diff }] }` or `{ applied: true, modifiedFiles: [] }`
- **CRITICAL**: Must handle multi-file changes (refactorings can span multiple files)

**Implementation Approach**:
1. Store a mapping of action IDs to actual `CodeAction` instances
   - Currently we only return `CodeActionInfo` (ID, title, kind)
   - Need to maintain a cache: `Dictionary<string, CodeAction>`
   - Consider cache expiration/cleanup strategy
2. Retrieve the `CodeAction` by ID from cache
3. Call `action.GetOperationsAsync()` to get operations
4. Extract `ApplyChangesOperation` which contains the modified solution
5. If preview: generate diffs for each changed document
6. If not preview: apply changes using `Workspace.TryApplyChanges()`
7. Handle edge cases:
   - Action not found in cache
   - Action expired/stale
   - Multi-file changes
   - Workspace conflicts

**Key Roslyn APIs**:
```csharp
var operations = await codeAction.GetOperationsAsync(cancellationToken);
foreach (var operation in operations.OfType<ApplyChangesOperation>())
{
    var newSolution = operation.ChangedSolution;
    // Compare with current solution to generate diffs
    // or apply via workspace.TryApplyChanges(newSolution)
}
```

**Testing Strategy**:
- Add integration test that:
  1. Loads a solution with a fixable diagnostic
  2. Gets code actions
  3. Applies a code action (preview mode)
  4. Verifies diff is correct
  5. Applies a code action (apply mode)
  6. Verifies file is actually modified

### Priority 2: Add Configuration File Support (Medium Priority)

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

### Priority 3: Pre-1.0 Usability Reviews (Medium Priority)

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
3. Start with implementing `apply_code_action` tool (highest priority)
4. Reference the "Implementation Approach" section above
5. Add integration tests for the new tool
6. Run all tests before committing

## Questions to Resolve

1. **Code action cache strategy**: How long should we cache `CodeAction` instances?
   - Option A: Cache for duration of session (simplest)
   - Option B: Cache with expiration (e.g., 5 minutes)
   - Option C: Cache per-document with invalidation on document changes

2. **Action ID generation**: Should we use `EquivalenceKey` or generate our own?
   - Current: `fix_{EquivalenceKey ?? Guid.NewGuid()}`
   - Issue: GUIDs make actions non-reproducible across calls

3. **Diff format**: What format should preview diffs use?
   - Unified diff?
   - Side-by-side?
   - Simple before/after?

4. **Multi-file handling**: How to present multi-file changes to LLM?
   - Return all diffs in single response?
   - Paginate?
   - Summary + details on request?

## Git Status

- Working tree clean
- Latest commit: `4cff02d` - Add MEF-based code action discovery with smart filtering
- Branch: `main`
- All changes committed

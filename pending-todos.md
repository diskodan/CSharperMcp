# Pending Tasks - Session Summary

Generated: 2026-01-18 | Updated: 2026-01-19

> **📋 For detailed context to resume in a new session, see [SESSION-CONTEXT.md](SESSION-CONTEXT.md)**

## Completed Tasks ✅

1. ✅ Convert methods to expression-bodied members where appropriate
2. ✅ Add TaskTupleAwaiter package and use parallel await syntax
3. ✅ Add OrEmpty() extension methods for collections
4. ✅ Convert configuration to IOptions pattern
5. ✅ Moved architectural todos to CLAUDE.md:
   - MEF-based code action discovery (documented in Phase 3)
   - apply_code_action implementation notes (documented in Phase 3)
   - Configuration file support (new section)

## Current Session Work

### 1. Switch to MEF-based code action discovery ✅
**Priority**: High (Phase 3 requirement)
**Status**: Completed
**Details**:
- Added System.Composition packages (v9.0.0) to support MEF v2
- Created `CodeActionProviderService` for dynamic provider discovery
- Updated `CodeActionsService` to use MEF-based discovery
- Discovers both `CodeFixProvider` and `CodeRefactoringProvider` instances
- All 43 tests passing

### 2. Add code action filtering/curation system ✅
**Priority**: High (reduces noise for LLM usage)
**Status**: Completed
**Details**:
- Created `CodeActionFilterConfiguration` model with sensible defaults
- Filters out noisy/subjective style suggestions (IDE0001, IDE0002, IDE0055, etc.)
- Excludes low-value refactorings ("Sort usings", "Organize usings", etc.)
- Priority list for important diagnostics (CS0103, CS0246, CA1031, etc.)
- Deduplication of actions with identical titles
- Configurable max results (default: 50)
- Can disable refactorings entirely if needed

### 3. Implement apply_code_action tool ✅
**Priority**: High (Phase 3 requirement)
**Status**: Completed
**Dependencies**: MEF-based discovery ✅ (completed)
**Details**:
- Created `ApplyCodeActionResult` and `FileChange` models for response structure
- Added `CodeAction` caching in `CodeActionsService` to enable applying actions by ID
- Implemented `ApplyCodeActionAsync` method supporting both preview and apply modes
- Preview mode returns unified diffs without modifying files
- Apply mode persists changes to disk and updates the workspace solution
- Handles multi-file changes (added, modified, and removed documents)
- Added `UpdateCurrentSolution` and `Workspace` property to `WorkspaceManager`
- Created `ApplyCodeActionTool` MCP tool
- Added 5 new unit tests for model validation
- Added 4 new integration tests (2 skipped due to no actions at test location)
- All 50 tests passing (7 unit + 43 integration)

### 4. Add configuration file support for overriding tool descriptions
**Priority**: Medium (usability enhancement)
**Status**: Not started
**Details**: See CLAUDE.md "Configuration File Support" section for specification

## Pre-1.0 Review Tasks

### 4. Usability review: tool names
**Priority**: Medium (before 1.0 release)
**Description**: Review all MCP tool names (e.g., `initialize_workspace`, `get_diagnostics`, `get_code_actions`) and ensure they follow consistent naming conventions and are intuitive for LLM usage. Consider snake_case vs camelCase consistency.

### 5. Usability review: tool parameter names
**Priority**: Medium (before 1.0 release)
**Description**: Review all tool parameter names for consistency, clarity, and LLM-friendliness. Ensure descriptions are helpful and examples are provided where needed.

### 6. Usability review: return value names and structure
**Priority**: Medium (before 1.0 release)
**Description**: Review all tool return value structures to ensure they are:
- Consistent across tools
- Self-documenting with clear property names
- Provide sufficient context for LLMs to understand and use the data
- Follow .NET naming conventions (PascalCase for properties)

## Notes

- All tests passing (50 tests: 7 unit + 43 integration)
- Build succeeds with 9 pre-existing warnings
- Current phase: Phase 1-3 complete (workspace init, diagnostics, code actions, apply code actions)
- Next milestone: Add configuration file support (Phase 4)

## References

- Full specification: `csharp-lsp-mcp-spec.md`
- Development guidelines: `CLAUDE.md`
- Current branch: `main`

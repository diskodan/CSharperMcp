# Robustness Features

This document describes the robustness features implemented in CSharperMcp server.

## Timeouts

All async operations have timeouts to prevent indefinite hangs:

- **Quick operations** (30 seconds): `GetDiagnosticsAsync`
- **Default operations** (2 minutes): `GetSymbolInfoAsync`, `FindReferencesAsync`, `GetDefinitionAsync`, `GetTypeMembersAsync`, `GetCodeActionsAsync`
- **Long operations** (5 minutes): `InitializeAsync` (workspace loading)

Timeouts are defined in `Common/OperationTimeout.cs`.

## Cancellation Token Support

All async methods accept an optional `CancellationToken cancellationToken = default` parameter. This allows:
- External cancellation (e.g., from MCP client)
- Timeout enforcement via `OperationTimeout.CreateLinked()`
- Graceful shutdown

## Exception Handling

### Workspace Not Initialized

All service methods check `_workspaceManager.CurrentSolution == null` and throw:
```csharp
throw new InvalidOperationException("Workspace not initialized. Call initialize_workspace first.");
```

### Timeout Exceptions

When an operation times out, we catch `OperationCanceledException` and convert to `TimeoutException`:
```csharp
catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
{
    _logger.LogError("Operation timed out after {Timeout}", timeout);
    throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds");
}
```

### Cancellation

When externally cancelled:
```csharp
catch (OperationCanceledException)
{
    _logger.LogInformation("Operation was cancelled");
    throw; // Re-throw to propagate cancellation
}
```

### General Exceptions

All exceptions are logged and re-thrown:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error in operation");
    throw;
}
```

## Input Validation

All public methods validate inputs:
- Null/empty string checks for required parameters
- Range checks for line/column numbers (when used)
- Path existence checks in WorkspaceManager

## --workspace CLI Parameter

When started with `--workspace <path>`, the server:
1. Auto-initializes the workspace at startup
2. Stores the path in `WorkspaceConfiguration` singleton
3. Blocks `initialize_workspace` tool calls (returns error message)
4. This prevents accidental re-initialization and reduces context window usage

Usage:
```bash
dotnet run --project src/CSharperMcp.Server -- --workspace /path/to/solution
```

Or in Cursor/Claude Code `.mcp.json`:
```json
{
  "mcpServers": {
    "csharperMcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/CSharperMcp.Server", "--", "--workspace", "$(pwd)"]
    }
  }
}
```

## Resource Disposal

- `WorkspaceManager` implements `IDisposable` and disposes `MSBuildWorkspace`
- `CancellationTokenSource` instances use `using` statements for automatic disposal
- No resource leaks on cancellation or timeout

## TODO: Remaining Work

The following methods still need timeout/cancellation support:
- [x] WorkspaceManager.InitializeAsync
- [x] RoslynService.GetDiagnosticsAsync
- [ ] RoslynService.GetSymbolInfoAsync
- [ ] RoslynService.FindReferencesAsync
- [ ] RoslynService.GetDefinitionAsync
- [ ] RoslynService.GetTypeMembersAsync
- [ ] CodeActionsService.GetCodeActionsAsync
- [ ] DecompilerService.DecompileType (currently synchronous, could add timeout wrapper)

## Testing

Timeout behavior is difficult to test in unit/integration tests. Consider:
- Manual testing with very large solutions
- Stress tests with intentionally slow operations
- Integration tests with artificially short timeouts (not recommended for CI)

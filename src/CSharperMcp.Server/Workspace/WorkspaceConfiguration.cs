namespace CSharperMcp.Server.Workspace;

/// <summary>
/// Configuration for workspace initialization.
/// </summary>
internal class WorkspaceConfiguration
{
    /// <summary>
    /// Path to auto-initialize workspace from (provided via --workspace or --workspace-from-cwd CLI parameter).
    /// If set, the workspace is initialized at startup and initialize_workspace tool is hidden.
    /// </summary>
    public string? InitialWorkspacePath { get; set; }
}

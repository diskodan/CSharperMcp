using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Workspace;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class WorkspaceTool
{
    [McpServerTool]
    [Description("Initialize the C# workspace with a solution or project path. Call this before using other tools.")]
    public static async Task<WorkspaceInitResult> InitializeWorkspace(
        WorkspaceManager workspaceManager,
        WorkspaceConfiguration config,
        ILogger<WorkspaceManager> logger,
        [Description("Absolute path to .sln, .csproj, or directory containing them")] string path)
    {
        // Don't allow re-initialization if workspace was auto-initialized
        if (!string.IsNullOrEmpty(config.InitialWorkspacePath))
        {
            return new WorkspaceInitResult(
                Success: false,
                Message: $"Workspace already initialized from --workspace parameter: {config.InitialWorkspacePath}. Restart server to change workspace.",
                ProjectCount: 0
            );
        }

        try
        {
            logger.LogInformation("Initializing workspace at {Path}", path);
            var (success, message, projectCount) = await workspaceManager.InitializeAsync(path);

            return new WorkspaceInitResult(
                Success: success,
                Message: message,
                ProjectCount: projectCount
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize workspace");
            return new WorkspaceInitResult(
                Success: false,
                Message: ex.Message,
                ProjectCount: 0
            );
        }
    }
}

internal record WorkspaceInitResult(
    bool Success,
    string Message,
    int ProjectCount
);

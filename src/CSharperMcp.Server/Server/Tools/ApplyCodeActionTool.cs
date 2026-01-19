using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class ApplyCodeActionTool
{
    [McpServerTool]
    [Description("Apply a code action (fix or refactoring) identified by its ID. Use preview mode to see changes before applying them.")]
    public static async Task<string> ApplyCodeAction(
        CodeActionsService codeActionsService,
        ILogger<CodeActionsService> logger,
        [Description("The ID of the code action to apply (from get_code_actions)")] string actionId,
        [Description("Optional: if true, return a preview of changes without applying them. Default: false")] bool preview = false)
    {
        try
        {
            var result = await codeActionsService.ApplyCodeActionAsync(actionId, preview);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = result.ErrorMessage
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                preview,
                changesCount = result.Changes.Count,
                changes = result.Changes.Select(c => new
                {
                    filePath = c.FilePath,
                    hasOriginalContent = c.OriginalContent != null,
                    hasModifiedContent = c.ModifiedContent != null,
                    originalContent = preview ? c.OriginalContent : null,
                    modifiedContent = preview ? c.ModifiedContent : null,
                    diff = c.UnifiedDiff
                })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying code action");
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

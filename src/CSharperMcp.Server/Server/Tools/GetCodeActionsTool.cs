using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class GetCodeActionsTool
{
    [McpServerTool]
    [Description("Get available code actions (fixes and refactorings) at a location. Returns actions that can be applied to fix issues or refactor code.")]
    public static async Task<string> GetCodeActions(
        CodeActionsService codeActionsService,
        ILogger<CodeActionsService> logger,
        [Description("File path")] string file,
        [Description("Start line number (1-based)")] int line,
        [Description("Start column number (1-based)")] int? column = null,
        [Description("End line number (1-based), optional for range selection")] int? endLine = null,
        [Description("End column number (1-based), optional for range selection")] int? endColumn = null,
        [Description("Filter to specific diagnostic IDs, e.g. ['CS0103', 'IDE0005']")] string[]? diagnosticIds = null)
    {
        try
        {
            var actions = await codeActionsService.GetCodeActionsAsync(
                file,
                line,
                column,
                endLine,
                endColumn,
                diagnosticIds);

            var actionList = actions.ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = actionList.Count,
                actions = actionList.Select(a => new
                {
                    id = a.Id,
                    title = a.Title,
                    kind = a.Kind,
                    diagnosticIds = a.DiagnosticIds
                })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting code actions");
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

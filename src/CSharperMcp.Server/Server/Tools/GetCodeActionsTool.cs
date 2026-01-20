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
    [Description("Get available code actions (fixes and refactorings) for a file. Returns all actions if line is omitted, or actions at a specific location if line is provided. Supports pagination for large result sets.")]
    public static async Task<string> GetCodeActions(
        CodeActionsService codeActionsService,
        ILogger<CodeActionsService> logger,
        [Description("File path")] string file,
        [Description("Optional: specific line number (1-based). If omitted, returns all actions for the entire file.")] int? line = null,
        [Description("Optional: start column number (1-based) when line is specified")] int? column = null,
        [Description("Optional: end line number (1-based) for range selection")] int? endLine = null,
        [Description("Optional: end column number (1-based) for range selection")] int? endColumn = null,
        [Description("Optional: filter to specific diagnostic IDs, e.g. ['CS0103', 'IDE0005']")] string[]? diagnosticIds = null,
        [Description("Optional: maximum number of results to return (default: 50)")] int maxResults = 50,
        [Description("Optional: number of results to skip (for pagination, default: 0)")] int offset = 0)
    {
        try
        {
            var result = await codeActionsService.GetCodeActionsAsync(
                file,
                line,
                column,
                endLine,
                endColumn,
                diagnosticIds,
                maxResults,
                offset);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = result.TotalCount,
                returnedCount = result.ReturnedCount,
                hasMore = result.HasMore,
                actions = result.Actions.Select(a => new
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

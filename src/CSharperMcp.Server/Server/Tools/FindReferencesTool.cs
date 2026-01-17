using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class FindReferencesTool
{
    [McpServerTool]
    [Description("Find all references to a symbol across the workspace. Returns file locations with line numbers and code snippets.")]
    public static async Task<string> FindReferences(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from (for location-based lookup)")] string? file = null,
        [Description("Line number (1-based) for location-based lookup")] int? line = null,
        [Description("Column number (1-based) for location-based lookup")] int? column = null,
        [Description("Fully qualified symbol name for name-based lookup (e.g. 'System.String')")] string? symbolName = null)
    {
        try
        {
            var references = await roslynService.FindReferencesAsync(file, line, column, symbolName);
            var referenceList = references.ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = referenceList.Count,
                references = referenceList
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding references");
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

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
    [Description("Find all references to a symbol across the workspace. Returns file locations with line numbers and code snippets. Supports pagination for large result sets. Use either (file + line + column) OR symbolName, not both.")]
    public static async Task<string> FindReferences(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from (for location-based lookup). Use with line and column parameters.")] string? file = null,
        [Description("Line number (1-based) for location-based lookup. Use with file and column parameters.")] int? line = null,
        [Description("Column number (1-based) for location-based lookup. Use with file and line parameters.")] int? column = null,
        [Description("Fully qualified symbol name for name-based lookup (e.g. 'System.String'). Do not use with file/line/column parameters.")] string? symbolName = null,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
        [Description("Number of lines of context around each reference (1 = current line only, 2 = 1 before + current, 3 = 1 before + current + 1 after, etc. Default: 1)")] int contextLines = 1)
    {
        try
        {
            // Validate mutually exclusive parameters
            bool hasLocation = file != null && line.HasValue && column.HasValue;
            bool hasPartialLocation = file != null || line.HasValue || column.HasValue;
            bool hasSymbolName = !string.IsNullOrEmpty(symbolName);

            // Check for conflicting parameters first (symbolName + any location parameter)
            if (hasSymbolName && hasPartialLocation)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Provide either (file + line + column) OR symbolName, not both"
                });
            }

            // Check for incomplete location parameters (but only if there's some location info provided)
            if (hasPartialLocation && !hasLocation && !hasSymbolName)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "When using location-based lookup, you must provide all three parameters: file, line, and column"
                });
            }

            // Check for missing parameters entirely
            if (!hasLocation && !hasSymbolName)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Must provide either (file + line + column) OR symbolName"
                });
            }

            var result = await roslynService.FindReferencesAsync(file, line, column, symbolName, maxResults, offset, contextLines);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = result.TotalCount,
                returnedCount = result.References.Count,
                hasMore = result.HasMore,
                references = result.References
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding references");
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

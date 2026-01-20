using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class SymbolInfoTool
{
    [McpServerTool]
    [Description("Get symbol information at a specific location or by fully qualified name. Like LSP hover - works for variables, methods, types, etc. Returns type, namespace, assembly, package, and signature. Set includeDocumentation=true to get XML doc comments (can be verbose). IMPORTANT: 'assembly' field is always present (project name for workspace symbols, DLL name for BCL/NuGet). 'package' field is only populated for NuGet packages (null for workspace and BCL). For workspace symbols, use SourceFile/SourceLine to navigate to definition. Use either (file + line + column) OR symbolName, not both.")]
    public static async Task<string> GetSymbolInfo(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from (for location-based lookup). Use with line and column parameters.")] string? file = null,
        [Description("Line number (1-based) for location-based lookup. Use with file and column parameters.")] int? line = null,
        [Description("Column number (1-based) for location-based lookup. Use with file and line parameters.")] int? column = null,
        [Description("Fully qualified symbol name for name-based lookup (e.g. 'System.String' or 'Newtonsoft.Json.Linq.JObject'). Do not use with file/line/column parameters.")] string? symbolName = null,
        [Description("Include XML documentation comments (default false to reduce token usage)")] bool includeDocumentation = false)
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

            var symbolInfo = await roslynService.GetSymbolInfoAsync(file, line, column, symbolName, includeDocumentation);

            if (symbolInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, message = "Symbol not found" });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                symbol = symbolInfo
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting symbol info");
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

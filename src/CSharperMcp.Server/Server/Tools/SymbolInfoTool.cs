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
    [Description("Get symbol information at a specific location. Like LSP hover - works for variables, methods, types, etc. Returns type, namespace, assembly, package, and signature. Set includeDocumentation=true to get XML doc comments (can be verbose). IMPORTANT: 'assembly' field is always present (project name for workspace symbols, DLL name for BCL/NuGet). 'package' field is only populated for NuGet packages (null for workspace and BCL). For workspace symbols, use SourceFile/SourceLine to navigate to definition.")]
    public static async Task<string> GetSymbolInfo(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from")] string file,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Include XML documentation comments (default false to reduce token usage)")] bool includeDocumentation = false)
    {
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(file))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "file parameter is required"
                });
            }

            var symbolInfo = await roslynService.GetSymbolInfoAsync(file, line, column, symbolName: null, includeDocumentation);

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

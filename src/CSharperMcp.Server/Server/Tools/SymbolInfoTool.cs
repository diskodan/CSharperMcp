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
    [Description("Get symbol information at a specific location or by fully qualified name. Returns type, namespace, assembly, documentation, and signature.")]
    public static async Task<string> GetSymbolInfo(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from (for location-based lookup)")] string? file = null,
        [Description("Line number (1-based) for location-based lookup")] int? line = null,
        [Description("Column number (1-based) for location-based lookup")] int? column = null,
        [Description("Fully qualified symbol name for name-based lookup (e.g. 'System.String' or 'Newtonsoft.Json.Linq.JObject')")] string? symbolName = null)
    {
        try
        {
            var symbolInfo = await roslynService.GetSymbolInfoAsync(file, line, column, symbolName);

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

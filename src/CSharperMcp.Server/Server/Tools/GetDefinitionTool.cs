using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class GetDefinitionTool
{
    [McpServerTool]
    [Description("Go to the definition of a symbol. Returns source file location for workspace symbols, or decompiled source code for DLL types (BCL, NuGet packages). Use either (file + line + column) OR symbolName, not both.")]
    public static async Task<string> GetDefinition(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from (for location-based lookup). Use with line and column parameters.")] string? file = null,
        [Description("Line number (1-based) for location-based lookup. Use with file and column parameters.")] int? line = null,
        [Description("Column number (1-based) for location-based lookup. Use with file and line parameters.")] int? column = null,
        [Description("Fully qualified symbol name for name-based lookup (e.g. 'System.String'). Do not use with file/line/column parameters.")] string? symbolName = null)
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

            var definition = await roslynService.GetDefinitionAsync(file, line, column, symbolName);

            if (definition == null)
            {
                return JsonSerializer.Serialize(new { success = false, message = "Symbol not found" });
            }

            if (definition.IsSourceLocation)
            {
                // Workspace symbol - return file location
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    isSourceLocation = true,
                    filePath = definition.FilePath,
                    line = definition.Line,
                    column = definition.Column,
                    assembly = definition.Assembly
                });
            }
            else
            {
                // DLL symbol - return decompiled source
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    isSourceLocation = false,
                    decompiledSource = definition.DecompiledSource,
                    assembly = definition.Assembly,
                    package = definition.Package
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting definition");
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

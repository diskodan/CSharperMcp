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
    [Description("Go to the definition of a symbol. Returns source file location for workspace symbols, or decompiled source code for DLL types (BCL, NuGet packages).")]
    public static async Task<string> GetDefinition(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("File path to get symbol from (for location-based lookup)")] string? file = null,
        [Description("Line number (1-based) for location-based lookup")] int? line = null,
        [Description("Column number (1-based) for location-based lookup")] int? column = null,
        [Description("Fully qualified symbol name for name-based lookup (e.g. 'System.String')")] string? symbolName = null)
    {
        try
        {
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

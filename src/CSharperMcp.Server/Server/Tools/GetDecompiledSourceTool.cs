using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class GetDecompiledSourceTool
{
    [McpServerTool]
    [Description("Get decompiled C# source code for a type from a DLL (BCL, NuGet package, etc.). By default returns reference-assembly style output (type signature, member signatures, and documentation comments, but no method bodies). This is token-efficient and useful for understanding APIs. Set includeImplementation=true to get full source with method bodies (WARNING: can be very large for complex types like System.String or Dictionary).")]
    public static async Task<string> GetDecompiledSource(
        RoslynService roslynService,
        DecompilerService decompilerService,
        ILogger<RoslynService> logger,
        [Description("Fully qualified type name (e.g. 'System.String', 'System.Collections.Generic.Dictionary`2')")] string typeName,
        [Description("Optional assembly name to help locate the type if ambiguous (e.g. 'System.Private.CoreLib')")] string? assembly = null,
        [Description("Include method bodies (default false). When false, returns only signatures + docs (reference assembly style).")] bool includeImplementation = false)
    {
        try
        {
            // Validate required parameter
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "typeName parameter is required"
                });
            }

            // Use RoslynService to resolve the symbol and find its assembly
            var symbolInfo = await roslynService.GetSymbolInfoAsync(symbolName: typeName, includeDocumentation: false);

            if (symbolInfo == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Type '{typeName}' not found. Ensure the workspace is initialized and the type is referenced by the loaded projects."
                });
            }

            // Check if this is a workspace symbol - we can't decompile those
            if (symbolInfo.IsFromWorkspace)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Type '{typeName}' is from workspace source code. Use get_type_members to view workspace types. This tool is only for decompiling DLL types."
                });
            }

            // Attempt to decompile the type
            var result = await decompilerService.DecompileTypeAsync(
                typeName,
                symbolInfo.Assembly,
                includeImplementation);

            if (result == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Failed to decompile type '{typeName}' from assembly '{symbolInfo.Assembly}'. The type may not be available or the assembly may be obfuscated."
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                typeName = result.TypeName,
                @namespace = result.Namespace,
                assembly = result.Assembly,
                package = result.Package,
                decompiledSource = result.DecompiledSource,
                includesImplementation = result.IncludesImplementation,
                lineCount = result.LineCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error decompiling source for type {TypeName}", typeName);
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

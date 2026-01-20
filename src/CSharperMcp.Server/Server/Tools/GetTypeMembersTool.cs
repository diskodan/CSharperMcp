using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class GetTypeMembersTool
{
    [McpServerTool]
    [Description("Get the full definition of a type with all its members. Universal tool that works for ANY type regardless of source - workspace code, BCL, or NuGet packages. Returns complete source code for workspace types or decompiled source for DLL types. Defaults to full implementation (includeImplementation=true). For token-efficient DLL exploration, use get_decompiled_source instead (defaults to signatures-only).")]
    public static async Task<string> GetTypeMembers(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("Fully qualified type name (e.g. 'System.String', 'SimpleProject.Calculator')")] string typeName,
        [Description("Include method implementations (default: true). Set to false for signatures only (more token-efficient for large types)")] bool includeImplementation = true)
    {
        try
        {
            var typeMembers = await roslynService.GetTypeMembersAsync(typeName, includeImplementation);

            if (typeMembers == null)
            {
                return JsonSerializer.Serialize(new { success = false, message = "Type not found" });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                typeName = typeMembers.TypeName,
                @namespace = typeMembers.Namespace,
                assembly = typeMembers.Assembly,
                package = typeMembers.Package,
                isFromWorkspace = typeMembers.IsFromWorkspace,
                filePath = typeMembers.FilePath,
                sourceCode = typeMembers.SourceCode,
                includesImplementation = typeMembers.IncludesImplementation,
                lineCount = typeMembers.LineCount,
                isLikelyObfuscated = typeMembers.IsLikelyObfuscated,
                obfuscationWarning = typeMembers.ObfuscationWarning
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting type members for {TypeName}", typeName);
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

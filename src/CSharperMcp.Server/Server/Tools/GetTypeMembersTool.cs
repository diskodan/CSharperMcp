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
    [Description("Get the full definition of a type with all its members. Returns complete source code for workspace types or decompiled source for DLL types (BCL, NuGet packages).")]
    public static async Task<string> GetTypeMembers(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("Fully qualified type name (e.g. 'System.String', 'SimpleProject.Calculator')")] string typeName,
        [Description("Include inherited members (not yet implemented, reserved for future use)")] bool includeInherited = false)
    {
        try
        {
            var typeMembers = await roslynService.GetTypeMembersAsync(typeName, includeInherited);

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
                sourceCode = typeMembers.SourceCode
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting type members for {TypeName}", typeName);
            return JsonSerializer.Serialize(new { success = false, message = ex.Message });
        }
    }
}

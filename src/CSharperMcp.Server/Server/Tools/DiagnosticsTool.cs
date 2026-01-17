using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Services;

namespace CSharperMcp.Server.Tools;

[McpServerToolType]
internal static class DiagnosticsTool
{
    [McpServerTool]
    [Description("Get compiler diagnostics (errors, warnings) for the workspace, a specific file, or a line range. Call initialize_workspace first.")]
    public static async Task<DiagnosticResult> GetDiagnostics(
        RoslynService roslynService,
        ILogger<RoslynService> logger,
        [Description("Optional file path to filter diagnostics to a specific file")] string? file = null,
        [Description("Optional start line (1-based) to filter diagnostics")] int? startLine = null,
        [Description("Optional end line (1-based) to filter diagnostics")] int? endLine = null,
        [Description("Minimum severity: error, warning, info, or hidden")] string severity = "warning")
    {
        try
        {
            var minimumSeverity = severity.ToLowerInvariant() switch
            {
                "error" => DiagnosticSeverity.Error,
                "warning" => DiagnosticSeverity.Warning,
                "info" => DiagnosticSeverity.Info,
                "hidden" => DiagnosticSeverity.Hidden,
                _ => DiagnosticSeverity.Warning
            };

            var diagnostics = await roslynService.GetDiagnosticsAsync(file, startLine, endLine, minimumSeverity);

            var diagnosticInfos = diagnostics.Select(d =>
            {
                var lineSpan = d.Location.GetLineSpan();
                return new DiagnosticInfo(
                    Id: d.Id,
                    Message: d.GetMessage(),
                    Severity: d.Severity.ToString(),
                    File: d.Location.SourceTree?.FilePath,
                    Line: lineSpan.StartLinePosition.Line + 1,
                    Column: lineSpan.StartLinePosition.Character + 1,
                    EndLine: lineSpan.EndLinePosition.Line + 1,
                    EndColumn: lineSpan.EndLinePosition.Character + 1,
                    Category: d.Descriptor.Category,
                    HasFix: false // TODO: Check for available code fixes
                );
            }).ToList();

            return new DiagnosticResult(
                Success: true,
                Diagnostics: diagnosticInfos,
                TotalCount: diagnosticInfos.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get diagnostics");
            return new DiagnosticResult(
                Success: false,
                Diagnostics: new List<DiagnosticInfo>(),
                TotalCount: 0,
                Error: ex.Message
            );
        }
    }
}

internal record DiagnosticResult(
    bool Success,
    List<DiagnosticInfo> Diagnostics,
    int TotalCount,
    string? Error = null
);

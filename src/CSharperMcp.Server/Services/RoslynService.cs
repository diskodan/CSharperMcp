using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using CSharperMcp.Server.Workspace;

namespace CSharperMcp.Server.Services;

internal class RoslynService
{
    private readonly WorkspaceManager _workspaceManager;
    private readonly ILogger<RoslynService> _logger;

    public RoslynService(WorkspaceManager workspaceManager, ILogger<RoslynService> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
    }

    public async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(
        string? filePath = null,
        int? startLine = null,
        int? endLine = null,
        DiagnosticSeverity minimumSeverity = DiagnosticSeverity.Warning)
    {
        if (_workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        var diagnostics = new List<Diagnostic>();

        foreach (var project in _workspaceManager.CurrentSolution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            var projectDiagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity >= minimumSeverity);

            // Filter by file if specified
            if (!string.IsNullOrEmpty(filePath))
            {
                projectDiagnostics = projectDiagnostics.Where(d =>
                    d.Location.SourceTree?.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Filter by line range if specified
            if (startLine.HasValue || endLine.HasValue)
            {
                projectDiagnostics = projectDiagnostics.Where(d =>
                {
                    var lineSpan = d.Location.GetLineSpan();
                    var diagStartLine = lineSpan.StartLinePosition.Line + 1; // Convert to 1-based
                    var diagEndLine = lineSpan.EndLinePosition.Line + 1;

                    if (startLine.HasValue && diagEndLine < startLine.Value) return false;
                    if (endLine.HasValue && diagStartLine > endLine.Value) return false;

                    return true;
                });
            }

            diagnostics.AddRange(projectDiagnostics);
        }

        return diagnostics;
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Workspace;

namespace CSharperMcp.Server.Services;

/// <summary>
/// Service for discovering and applying code actions (fixes and refactorings).
/// Note: This is a simplified implementation. Full code action support requires
/// complex MEF composition and provider registration that's beyond the scope
/// of an initial MCP server implementation.
/// </summary>
internal class CodeActionsService
{
    private readonly WorkspaceManager _workspaceManager;
    private readonly ILogger<CodeActionsService> _logger;

    // Common fixable diagnostic IDs (this is a simplified subset)
    private static readonly Dictionary<string, string> _knownFixableDiagnostics = new()
    {
        ["CS0246"] = "Add using directive or assembly reference",
        ["CS0103"] = "The name does not exist in the current context",
        ["CS0128"] = "Remove unused local variable",
        ["CS0219"] = "Remove unused variable",
        ["CS0414"] = "Remove unused private field",
        ["CS0618"] = "Member is obsolete",
        ["CS1061"] = "Add using directive for extension methods",
        ["IDE0005"] = "Remove unnecessary using directive",
        ["IDE0051"] = "Remove unused private member",
        ["IDE0052"] = "Remove unread private member",
        ["IDE0058"] = "Remove unnecessary expression value",
        ["IDE0059"] = "Remove unnecessary value assignment",
        ["IDE0060"] = "Remove unused parameter",
    };

    public CodeActionsService(WorkspaceManager workspaceManager, ILogger<CodeActionsService> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
    }

    public async Task<IEnumerable<CodeActionInfo>> GetCodeActionsAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        int? endLine = null,
        int? endColumn = null,
        IEnumerable<string>? diagnosticIds = null)
    {
        if (_workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        var actions = new List<CodeActionInfo>();

        // Find the document
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogWarning("File path is required for getting code actions");
            return actions;
        }

        Document? document = null;
        foreach (var project in _workspaceManager.CurrentSolution.Projects)
        {
            var doc = project.Documents.FirstOrDefault(d =>
                d.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) == true);
            if (doc != null)
            {
                document = doc;
                break;
            }
        }

        if (document == null)
        {
            _logger.LogWarning("Document not found: {FilePath}", filePath);
            return actions;
        }

        // Get semantic model for diagnostics
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
        {
            _logger.LogWarning("Could not get semantic model for {FilePath}", filePath);
            return actions;
        }

        // Get all diagnostics for the document
        var allDiagnostics = semanticModel.GetDiagnostics();

        // Filter diagnostics based on parameters
        IEnumerable<Diagnostic> relevantDiagnostics;

        if (line.HasValue)
        {
            // If line is specified, filter to that location
            var sourceText = await document.GetTextAsync();
            var startPosition = sourceText.Lines[line.Value - 1].Start + (column ?? 1) - 1;
            var endPosition = endLine.HasValue && endColumn.HasValue
                ? sourceText.Lines[endLine.Value - 1].Start + endColumn.Value - 1
                : startPosition;

            var textSpan = TextSpan.FromBounds(startPosition, endPosition);
            relevantDiagnostics = allDiagnostics.Where(d => d.Location.SourceSpan.IntersectsWith(textSpan));
        }
        else
        {
            // No line specified - return all diagnostics for the file
            relevantDiagnostics = allDiagnostics;
        }

        // Apply diagnostic ID filter if specified
        if (diagnosticIds != null && diagnosticIds.Any())
        {
            relevantDiagnostics = relevantDiagnostics.Where(d => diagnosticIds.Contains(d.Id));
        }

        var relevantDiagnosticsList = relevantDiagnostics.ToList();

        // For each diagnostic, check if we know about fixes
        foreach (var diagnostic in relevantDiagnostics)
        {
            if (_knownFixableDiagnostics.TryGetValue(diagnostic.Id, out var fixDescription))
            {
                actions.Add(new CodeActionInfo(
                    Id: $"fix_{diagnostic.Id}_{diagnostic.Location.SourceSpan.Start}",
                    Title: fixDescription,
                    Kind: "QuickFix",
                    DiagnosticIds: new[] { diagnostic.Id }
                ));
            }
        }

        // Note: Full refactoring support would require:
        // 1. MEF composition to discover CodeRefactoringProviders
        // 2. Calling ComputeRefactoringsAsync on each provider
        // 3. Collecting all the returned actions
        // This is complex and beyond the initial MCP server scope.
        // For now, we return diagnostic-based fixes only.

        _logger.LogInformation("Found {Count} code actions at {FilePath}:{Line}", actions.Count, filePath, line);
        return actions;
    }
}

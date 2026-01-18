using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CSharperMcp.Server.Extensions;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Workspace;

namespace CSharperMcp.Server.Services;

/// <summary>
/// Service for discovering and applying code actions (fixes and refactorings).
/// Uses MEF-based provider discovery to dynamically find all available CodeFixProviders
/// and CodeRefactoringProviders from Roslyn and analyzer packages.
/// Filters code actions based on configuration to reduce noise and improve LLM usability.
/// </summary>
internal class CodeActionsService(
    WorkspaceManager workspaceManager,
    CodeActionProviderService providerService,
    IOptions<CodeActionFilterConfiguration> filterConfig,
    ILogger<CodeActionsService> logger)
{
    private readonly CodeActionFilterConfiguration _filterConfig = filterConfig.Value;

    public async Task<IEnumerable<CodeActionInfo>> GetCodeActionsAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        int? endLine = null,
        int? endColumn = null,
        IEnumerable<string>? diagnosticIds = null)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        var actions = new List<CodeActionInfo>();

        // Find the document
        if (filePath.IsNullOrEmpty())
        {
            logger.LogWarning("File path is required for getting code actions");
            return actions;
        }

        Document? document = null;
        foreach (var project in workspaceManager.CurrentSolution.Projects)
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
            logger.LogWarning("Document not found: {FilePath}", filePath);
            return actions;
        }

        // Determine text span for code actions
        var sourceText = await document.GetTextAsync();
        TextSpan textSpan;

        if (line.HasValue)
        {
            var startPosition = sourceText.Lines[line.Value - 1].Start + (column ?? 1) - 1;
            var endPosition = endLine.HasValue && endColumn.HasValue
                ? sourceText.Lines[endLine.Value - 1].Start + endColumn.Value - 1
                : startPosition;

            textSpan = TextSpan.FromBounds(startPosition, endPosition);
        }
        else
        {
            // No line specified - use entire document span
            textSpan = new TextSpan(0, sourceText.Length);
        }

        // Get diagnostics for the document
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
        {
            logger.LogWarning("Could not get semantic model for {FilePath}", filePath);
            return actions;
        }

        var allDiagnostics = semanticModel.GetDiagnostics();
        var relevantDiagnostics = allDiagnostics.Where(d => d.Location.SourceSpan.IntersectsWith(textSpan));

        // Apply diagnostic ID filter if specified
        if (diagnosticIds != null && diagnosticIds.Any())
        {
            relevantDiagnostics = relevantDiagnostics.Where(d => diagnosticIds.Contains(d.Id));
        }

        var relevantDiagnosticsList = relevantDiagnostics.ToImmutableArray();

        // Get code fix providers for this project
        var codeFixProviders = providerService.GetCodeFixProviders(document.Project);

        // Collect code fixes from all providers
        var codeFixTasks = codeFixProviders.Select(async provider =>
        {
            var fixableDiagnostics = relevantDiagnosticsList
                .AsEnumerable()
                .Where(d => provider.FixableDiagnosticIds.Contains(d.Id))
                .ToImmutableArray();

            if (fixableDiagnostics.IsEmpty)
                return Enumerable.Empty<(CodeAction Action, ImmutableArray<string> DiagnosticIds)>();

            var collectedActions = new List<(CodeAction Action, ImmutableArray<string> DiagnosticIds)>();

            var context = new CodeFixContext(
                document,
                textSpan,
                fixableDiagnostics,
                (action, applicableDiagnostics) =>
                {
                    var diagnosticIdsForAction = applicableDiagnostics.Select(d => d.Id).ToImmutableArray();
                    collectedActions.Add((action, diagnosticIdsForAction));
                },
                CancellationToken.None);

            try
            {
                await provider.RegisterCodeFixesAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CodeFixProvider {ProviderName} threw exception", provider.GetType().Name);
            }

            return collectedActions.AsEnumerable();
        }).ToArray();

        var allCodeFixResults = await Task.WhenAll((IEnumerable<Task<IEnumerable<(CodeAction Action, ImmutableArray<string> DiagnosticIds)>>>)codeFixTasks);
        var allCodeFixes = allCodeFixResults.SelectMany(x => x).ToList();

        // Convert CodeActions to CodeActionInfo
        foreach (var (action, diagnosticIdsForAction) in allCodeFixes)
        {
            actions.Add(new CodeActionInfo(
                Id: $"fix_{action.EquivalenceKey ?? Guid.NewGuid().ToString()}",
                Title: action.Title,
                Kind: "QuickFix",
                DiagnosticIds: diagnosticIdsForAction.ToArray()
            ));
        }

        // Get code refactoring providers for this project
        var refactoringProviders = providerService.GetCodeRefactoringProviders(document.Project);

        // Collect refactorings from all providers
        var refactoringTasks = refactoringProviders.Select(async provider =>
        {
            var collectedRefactorings = new List<CodeAction>();

            var context = new CodeRefactoringContext(
                document,
                textSpan,
                collectedRefactorings.Add,
                CancellationToken.None);

            try
            {
                await provider.ComputeRefactoringsAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CodeRefactoringProvider {ProviderName} threw exception", provider.GetType().Name);
            }

            return collectedRefactorings.AsEnumerable();
        }).ToArray();

        var allRefactoringResults = await Task.WhenAll((IEnumerable<Task<IEnumerable<CodeAction>>>)refactoringTasks);
        var allRefactorings = allRefactoringResults.SelectMany(x => x).ToList();

        // Convert refactorings to CodeActionInfo (if enabled)
        if (_filterConfig.IncludeRefactorings)
        {
            foreach (var action in allRefactorings)
            {
                actions.Add(new CodeActionInfo(
                    Id: $"refactor_{action.EquivalenceKey ?? Guid.NewGuid().ToString()}",
                    Title: action.Title,
                    Kind: "Refactor",
                    DiagnosticIds: Array.Empty<string>()
                ));
            }
        }

        // Apply filters to reduce noise
        var filteredActions = ApplyFilters(actions);

        logger.LogInformation("Found {Count} code actions at {FilePath}:{Line} ({FixCount} fixes, {RefactorCount} refactorings, {FilteredCount} after filtering)",
            actions.Count,
            filePath,
            line,
            allCodeFixes.Count,
            allRefactorings.Count,
            filteredActions.Count);

        return filteredActions;
    }

    private List<CodeActionInfo> ApplyFilters(List<CodeActionInfo> actions)
    {
        var filtered = actions.AsEnumerable();

        // Filter out excluded diagnostic IDs (unless they're priority diagnostics)
        filtered = filtered.Where(action =>
        {
            // Refactorings have no diagnostic IDs, include them (they'll be filtered by pattern later)
            if (!action.DiagnosticIds.Any())
                return true;

            // Priority diagnostics always pass through
            if (action.DiagnosticIds.Any(id => _filterConfig.PriorityDiagnosticIds.Contains(id)))
                return true;

            // Exclude if any diagnostic ID is in the exclusion list
            return !action.DiagnosticIds.Any(id => _filterConfig.ExcludedDiagnosticIds.Contains(id));
        });

        // Filter out excluded refactoring patterns
        if (_filterConfig.ExcludedRefactoringPatterns.Count > 0)
        {
            filtered = filtered.Where(action =>
                !_filterConfig.ExcludedRefactoringPatterns.Any(pattern =>
                    action.Title.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
        }

        // Deduplicate by title if enabled
        if (_filterConfig.DeduplicateByTitle)
        {
            filtered = filtered
                .GroupBy(action => action.Title, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        // Limit results
        var result = filtered.Take(_filterConfig.MaxResults).ToList();

        if (result.Count < actions.Count)
        {
            logger.LogDebug("Filtered code actions from {OriginalCount} to {FilteredCount}",
                actions.Count,
                result.Count);
        }

        return result;
    }
}

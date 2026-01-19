using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
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
    private readonly ConcurrentDictionary<string, CodeAction> _codeActionCache = new();

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

        // Convert CodeActions to CodeActionInfo and cache them
        foreach (var (action, diagnosticIdsForAction) in allCodeFixes)
        {
            var actionId = $"fix_{action.EquivalenceKey ?? Guid.NewGuid().ToString()}";
            _codeActionCache[actionId] = action;

            actions.Add(new CodeActionInfo(
                Id: actionId,
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

        // Convert refactorings to CodeActionInfo (if enabled) and cache them
        if (_filterConfig.IncludeRefactorings)
        {
            foreach (var action in allRefactorings)
            {
                var actionId = $"refactor_{action.EquivalenceKey ?? Guid.NewGuid().ToString()}";
                _codeActionCache[actionId] = action;

                actions.Add(new CodeActionInfo(
                    Id: actionId,
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

    public async Task<ApplyCodeActionResult> ApplyCodeActionAsync(
        string actionId,
        bool preview = false)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            return new ApplyCodeActionResult(
                Success: false,
                ErrorMessage: "Workspace not initialized",
                Changes: Array.Empty<FileChange>()
            );
        }

        // Retrieve the CodeAction from cache
        if (!_codeActionCache.TryGetValue(actionId, out var codeAction))
        {
            return new ApplyCodeActionResult(
                Success: false,
                ErrorMessage: $"Code action with ID '{actionId}' not found. Call get_code_actions first to populate the cache.",
                Changes: Array.Empty<FileChange>()
            );
        }

        try
        {
            // Get the operations for this code action
            var operations = await codeAction.GetOperationsAsync(CancellationToken.None);

            var fileChanges = new List<FileChange>();
            Solution? newSolution = null;

            // Process each operation
            foreach (var operation in operations)
            {
                if (operation is ApplyChangesOperation applyChangesOperation)
                {
                    newSolution = applyChangesOperation.ChangedSolution;
                    break; // Typically there's only one ApplyChangesOperation
                }
            }

            if (newSolution == null)
            {
                return new ApplyCodeActionResult(
                    Success: false,
                    ErrorMessage: "Code action did not produce any changes",
                    Changes: Array.Empty<FileChange>()
                );
            }

            // Get the document changes
            var solutionChanges = newSolution.GetChanges(workspaceManager.CurrentSolution);

            foreach (var projectChanges in solutionChanges.GetProjectChanges())
            {
                // Handle changed documents
                foreach (var changedDocId in projectChanges.GetChangedDocuments())
                {
                    var oldDoc = workspaceManager.CurrentSolution.GetDocument(changedDocId);
                    var newDoc = newSolution.GetDocument(changedDocId);

                    if (oldDoc?.FilePath == null || newDoc == null)
                        continue;

                    var oldText = await oldDoc.GetTextAsync();
                    var newText = await newDoc.GetTextAsync();

                    var change = new FileChange(
                        FilePath: oldDoc.FilePath,
                        OriginalContent: oldText.ToString(),
                        ModifiedContent: newText.ToString(),
                        UnifiedDiff: GenerateUnifiedDiff(oldDoc.FilePath, oldText.ToString(), newText.ToString())
                    );

                    fileChanges.Add(change);
                }

                // Handle added documents
                foreach (var addedDocId in projectChanges.GetAddedDocuments())
                {
                    var newDoc = newSolution.GetDocument(addedDocId);

                    if (newDoc?.FilePath == null)
                        continue;

                    var newText = await newDoc.GetTextAsync();

                    var change = new FileChange(
                        FilePath: newDoc.FilePath,
                        OriginalContent: null,
                        ModifiedContent: newText.ToString(),
                        UnifiedDiff: GenerateUnifiedDiff(newDoc.FilePath, "", newText.ToString())
                    );

                    fileChanges.Add(change);
                }

                // Handle removed documents
                foreach (var removedDocId in projectChanges.GetRemovedDocuments())
                {
                    var oldDoc = workspaceManager.CurrentSolution.GetDocument(removedDocId);

                    if (oldDoc?.FilePath == null)
                        continue;

                    var oldText = await oldDoc.GetTextAsync();

                    var change = new FileChange(
                        FilePath: oldDoc.FilePath,
                        OriginalContent: oldText.ToString(),
                        ModifiedContent: null,
                        UnifiedDiff: GenerateUnifiedDiff(oldDoc.FilePath, oldText.ToString(), "")
                    );

                    fileChanges.Add(change);
                }
            }

            // If not preview mode, apply the changes to the workspace and persist to disk
            if (!preview)
            {
                if (workspaceManager.Workspace.TryApplyChanges(newSolution))
                {
                    // Update the current solution
                    workspaceManager.UpdateCurrentSolution(newSolution);

                    // Persist changes to disk
                    foreach (var change in fileChanges)
                    {
                        if (change.ModifiedContent != null)
                        {
                            await File.WriteAllTextAsync(change.FilePath, change.ModifiedContent);
                            logger.LogInformation("Applied code action to file: {FilePath}", change.FilePath);
                        }
                        else if (change.OriginalContent != null)
                        {
                            // File was removed
                            if (File.Exists(change.FilePath))
                            {
                                File.Delete(change.FilePath);
                                logger.LogInformation("Removed file: {FilePath}", change.FilePath);
                            }
                        }
                    }

                    logger.LogInformation("Successfully applied code action '{Title}' ({ActionId})", codeAction.Title, actionId);
                }
                else
                {
                    return new ApplyCodeActionResult(
                        Success: false,
                        ErrorMessage: "Failed to apply changes to workspace",
                        Changes: Array.Empty<FileChange>()
                    );
                }
            }

            return new ApplyCodeActionResult(
                Success: true,
                ErrorMessage: null,
                Changes: fileChanges
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying code action '{ActionId}'", actionId);
            return new ApplyCodeActionResult(
                Success: false,
                ErrorMessage: $"Error applying code action: {ex.Message}",
                Changes: Array.Empty<FileChange>()
            );
        }
    }

    private static string GenerateUnifiedDiff(string filePath, string originalContent, string modifiedContent)
    {
        var originalLines = originalContent.Split('\n');
        var modifiedLines = modifiedContent.Split('\n');

        var diff = new StringBuilder();
        diff.AppendLine($"--- {filePath}");
        diff.AppendLine($"+++ {filePath}");

        // Simple line-by-line diff (not a full unified diff algorithm, but sufficient for preview)
        var maxLines = Math.Max(originalLines.Length, modifiedLines.Length);
        var hunkStart = 0;
        var inHunk = false;

        for (int i = 0; i < maxLines; i++)
        {
            var oldLine = i < originalLines.Length ? originalLines[i] : null;
            var newLine = i < modifiedLines.Length ? modifiedLines[i] : null;

            if (oldLine != newLine)
            {
                if (!inHunk)
                {
                    hunkStart = Math.Max(0, i - 3);
                    diff.AppendLine($"@@ -{hunkStart + 1},{Math.Min(originalLines.Length - hunkStart, 10)} +{hunkStart + 1},{Math.Min(modifiedLines.Length - hunkStart, 10)} @@");
                    inHunk = true;
                }

                if (oldLine != null && newLine == null)
                {
                    diff.AppendLine($"-{oldLine}");
                }
                else if (oldLine == null && newLine != null)
                {
                    diff.AppendLine($"+{newLine}");
                }
                else
                {
                    diff.AppendLine($"-{oldLine}");
                    diff.AppendLine($"+{newLine}");
                }
            }
            else if (inHunk && oldLine != null)
            {
                diff.AppendLine($" {oldLine}");
            }
        }

        return diff.ToString();
    }
}

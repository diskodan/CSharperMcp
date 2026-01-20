using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using CSharperMcp.Server.Common;
using CSharperMcp.Server.Extensions;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Workspace;

namespace CSharperMcp.Server.Services;

internal class RoslynService(
    WorkspaceManager workspaceManager,
    DecompilerService decompilerService,
    ILogger<RoslynService> logger)
{

    public async Task<(IEnumerable<Diagnostic> diagnostics, int totalCount)> GetDiagnosticsAsync(
        string? filePath = null,
        int? startLine = null,
        int? endLine = null,
        DiagnosticSeverity minimumSeverity = DiagnosticSeverity.Warning,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized. Call initialize_workspace first.");
        }

        using var timeoutCts = OperationTimeout.CreateLinked(cancellationToken, OperationTimeout.Quick);
        var allDiagnostics = new List<Diagnostic>();

        try
        {
            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                timeoutCts.Token.ThrowIfCancellationRequested();

                var compilation = await project.GetCompilationAsync(timeoutCts.Token);
                if (compilation == null) continue;

                // Get compiler diagnostics (CS* errors/warnings)
                var compilerDiagnostics = compilation.GetDiagnostics();

                // Get analyzer diagnostics (IDE* suggestions, code style, etc.)
                // This includes all analyzers referenced by the project
                var analyzerDiagnostics = new List<Diagnostic>();
                if (compilation.Options.SpecificDiagnosticOptions.Any() || project.AnalyzerReferences.Any())
                {
                    try
                    {
                        var analyzers = project.AnalyzerReferences
                            .SelectMany(r => r.GetAnalyzersForAllLanguages())
                            .ToImmutableArray();

                        if (analyzers.Any())
                        {
                            var compilationWithAnalyzers = compilation.WithAnalyzers(
                                analyzers,
                                options: null); // Use default analyzer options
                            var analyzerResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync(timeoutCts.Token);
                            analyzerDiagnostics.AddRange(analyzerResults);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - analyzer diagnostics are nice-to-have
                        logger.LogWarning(ex, "Failed to get analyzer diagnostics for project {Project}", project.Name);
                    }
                }

                // Combine both types
                var projectDiagnostics = compilerDiagnostics
                    .Concat(analyzerDiagnostics)
                    .Where(d => d.Severity >= minimumSeverity);

            // Filter by file if specified
            if (!filePath.IsNullOrEmpty())
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

                allDiagnostics.AddRange(projectDiagnostics);
            }

            // Calculate total count before pagination
            var totalCount = allDiagnostics.Count;

            // Apply pagination
            var paginatedDiagnostics = allDiagnostics
                .Skip(offset)
                .Take(maxResults);

            return (paginatedDiagnostics, totalCount);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            logger.LogError("GetDiagnostics timed out after {Timeout}", OperationTimeout.Quick);
            throw new TimeoutException($"Operation timed out after {OperationTimeout.Quick.TotalSeconds} seconds");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("GetDiagnostics was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting diagnostics");
            throw;
        }
    }

    public virtual async Task<Models.SymbolInfo?> GetSymbolInfoAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? symbolName = null,
        bool includeDocumentation = false)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        ISymbol? symbol = null;

        // Get symbol by location
        if (filePath != null && line.HasValue && column.HasValue)
        {
            var document = FindDocument(filePath);
            if (document == null)
            {
                logger.LogWarning("Document not found: {FilePath}", filePath);
                return null;
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[line.Value - 1].Start + column.Value - 1; // Convert to 0-based

            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null) return null;

            var syntaxRoot = await document.GetSyntaxRootAsync();
            if (syntaxRoot == null) return null;

            var node = syntaxRoot.FindToken(position).Parent;
            if (node == null) return null;

            var symbolInfo = semanticModel.GetSymbolInfo(node);
            symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node);
        }
        // Get symbol by fully qualified name
        else if (!symbolName.IsNullOrEmpty())
        {
            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                symbol = compilation.GetTypeByMetadataName(symbolName);
                if (symbol != null) break;
            }
        }

        if (symbol == null) return null;

        return MapSymbolToSymbolInfo(symbol, includeDocumentation);
    }

    private Document? FindDocument(string filePath)
    {
        if (workspaceManager.CurrentSolution == null) return null;

        foreach (var project in workspaceManager.CurrentSolution.Projects)
        {
            var doc = project.Documents.FirstOrDefault(d =>
                d.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) == true);
            if (doc != null) return doc;
        }

        return null;
    }

    private static Models.SymbolInfo MapSymbolToSymbolInfo(ISymbol symbol, bool includeDocumentation)
    {
        var kind = symbol.Kind.ToString();
        var name = symbol.Name;
        var containingType = symbol.ContainingType?.ToDisplayString();
        var ns = symbol.ContainingNamespace?.ToDisplayString();
        if (ns == "<global namespace>") ns = null;

        var assembly = symbol.ContainingAssembly?.Name;
        string? package = null;

        // Try to determine NuGet package from assembly identity
        var assemblyIdentity = symbol.ContainingAssembly?.Identity;
        if (assemblyIdentity != null && !assemblyIdentity.IsRetargetable)
        {
            // For NuGet packages, the assembly name often matches the package name
            package = assembly;
        }

        // Only include documentation if requested
        var docComment = includeDocumentation ? symbol.GetDocumentationCommentXml() : null;
        var modifiers = GetModifiers(symbol);

        string? signature = null;
        if (symbol is IMethodSymbol method)
        {
            signature = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
        else if (symbol is IPropertySymbol property)
        {
            signature = property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }
        else if (symbol is ITypeSymbol type)
        {
            signature = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        // Determine if symbol is from workspace and get source location
        bool isFromWorkspace = false;
        string? sourceFile = null;
        int? sourceLine = null;
        int? sourceColumn = null;

        var locations = symbol.Locations;
        if (locations.Length > 0 && locations[0].IsInSource)
        {
            isFromWorkspace = true;
            var loc = locations[0];
            var lineSpan = loc.GetLineSpan();
            sourceFile = loc.SourceTree?.FilePath;
            sourceLine = lineSpan.StartLinePosition.Line + 1; // Convert to 1-based
            sourceColumn = lineSpan.StartLinePosition.Character + 1; // Convert to 1-based
        }

        return new Models.SymbolInfo(
            kind,
            name,
            containingType,
            ns,
            assembly,
            package,
            docComment,
            modifiers,
            signature,
            isFromWorkspace,
            sourceFile,
            sourceLine,
            sourceColumn
        );
    }

    private static IReadOnlyList<string> GetModifiers(ISymbol symbol)
    {
        var modifiers = new List<string>();

        if (symbol.IsStatic) modifiers.Add("static");
        if (symbol.IsVirtual) modifiers.Add("virtual");
        if (symbol.IsOverride) modifiers.Add("override");
        if (symbol.IsAbstract) modifiers.Add("abstract");
        if (symbol.IsSealed) modifiers.Add("sealed");

        modifiers.Add(symbol.DeclaredAccessibility.ToString().ToLowerInvariant());

        return modifiers;
    }

    public async Task<FindReferencesResult> FindReferencesAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? symbolName = null,
        int maxResults = 100,
        int offset = 0,
        int contextLines = 1)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        // Validate parameters
        if (maxResults < 1)
        {
            throw new ArgumentException("maxResults must be at least 1", nameof(maxResults));
        }

        if (offset < 0)
        {
            throw new ArgumentException("offset must be non-negative", nameof(offset));
        }

        if (contextLines < 1)
        {
            throw new ArgumentException("contextLines must be at least 1", nameof(contextLines));
        }

        ISymbol? symbol = null;

        // Get symbol by location
        if (filePath != null && line.HasValue && column.HasValue)
        {
            var document = FindDocument(filePath);
            if (document == null)
            {
                logger.LogWarning("Document not found: {FilePath}", filePath);
                return new FindReferencesResult(0, false, Array.Empty<ReferenceInfo>());
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[line.Value - 1].Start + column.Value - 1;

            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null) return new FindReferencesResult(0, false, Array.Empty<ReferenceInfo>());

            var syntaxRoot = await document.GetSyntaxRootAsync();
            if (syntaxRoot == null) return new FindReferencesResult(0, false, Array.Empty<ReferenceInfo>());

            var node = syntaxRoot.FindToken(position).Parent;
            if (node == null) return new FindReferencesResult(0, false, Array.Empty<ReferenceInfo>());

            var symbolInfo = semanticModel.GetSymbolInfo(node);
            symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node);
        }
        // Get symbol by fully qualified name
        else if (!symbolName.IsNullOrEmpty())
        {
            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                symbol = compilation.GetTypeByMetadataName(symbolName);
                if (symbol != null) break;
            }
        }

        if (symbol == null) return new FindReferencesResult(0, false, Array.Empty<ReferenceInfo>());

        // Find all references
        var references = await SymbolFinder.FindReferencesAsync(
            symbol,
            workspaceManager.CurrentSolution);

        var results = new List<ReferenceInfo>();

        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var doc = location.Document;
                var sourceText = await doc.GetTextAsync();
                var lineSpan = location.Location.GetLineSpan();

                var startLine = lineSpan.StartLinePosition.Line + 1;
                var startColumn = lineSpan.StartLinePosition.Character + 1;
                var endLine = lineSpan.EndLinePosition.Line + 1;
                var endColumn = lineSpan.EndLinePosition.Character + 1;

                // Get context snippet with surrounding lines
                var contextSnippet = GetContextSnippet(sourceText, lineSpan.StartLinePosition.Line, contextLines);

                var referenceKind = location.IsImplicit ? "Implicit" : "Explicit";

                results.Add(new ReferenceInfo(
                    doc.FilePath ?? doc.Name,
                    startLine,
                    startColumn,
                    endLine,
                    endColumn,
                    contextSnippet,
                    referenceKind
                ));
            }
        }

        // Apply pagination
        var totalCount = results.Count;
        var paginatedResults = results
            .Skip(offset)
            .Take(maxResults)
            .ToList();

        var hasMore = offset + paginatedResults.Count < totalCount;

        return new FindReferencesResult(totalCount, hasMore, paginatedResults);
    }

    private static string GetContextSnippet(SourceText sourceText, int referenceLine, int contextLines)
    {
        // Calculate how many lines before and after to include
        // contextLines=1: just the current line
        // contextLines=2: 1 before + current
        // contextLines=3: 1 before + current + 1 after
        // contextLines=4: 2 before + current + 1 after
        // etc.

        var linesBefore = (contextLines - 1) / 2;
        var linesAfter = contextLines - 1 - linesBefore;

        var startLine = Math.Max(0, referenceLine - linesBefore);
        var endLine = Math.Min(sourceText.Lines.Count - 1, referenceLine + linesAfter);

        var lines = new List<string>();
        for (var i = startLine; i <= endLine; i++)
        {
            lines.Add(sourceText.Lines[i].ToString().TrimEnd());
        }

        return string.Join(Environment.NewLine, lines);
    }

    public async Task<DefinitionInfo?> GetDefinitionAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? symbolName = null)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        ISymbol? symbol = null;

        // Get symbol by location
        if (filePath != null && line.HasValue && column.HasValue)
        {
            var document = FindDocument(filePath);
            if (document == null)
            {
                logger.LogWarning("Document not found: {FilePath}", filePath);
                return null;
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[line.Value - 1].Start + column.Value - 1;

            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null) return null;

            var syntaxRoot = await document.GetSyntaxRootAsync();
            if (syntaxRoot == null) return null;

            var node = syntaxRoot.FindToken(position).Parent;
            if (node == null) return null;

            var symbolInfo = semanticModel.GetSymbolInfo(node);
            symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node);
        }
        // Get symbol by fully qualified name
        else if (!symbolName.IsNullOrEmpty())
        {
            logger.LogInformation("Looking up symbol by name: {SymbolName}", symbolName);
            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                symbol = compilation.GetTypeByMetadataName(symbolName);
                if (symbol != null)
                {
                    logger.LogInformation("Found symbol {SymbolName} in project {ProjectName}", symbolName, project.Name);
                    break;
                }
            }

            if (symbol == null)
            {
                logger.LogWarning("Symbol {SymbolName} not found in any project", symbolName);
            }
        }

        if (symbol == null) return null;

        // Check if symbol is in source or metadata
        var locations = symbol.Locations;
        if (locations.Length > 0 && locations[0].IsInSource)
        {
            // Symbol is in workspace source - return file location
            var location = locations[0];
            var lineSpan = location.GetLineSpan();
            var sourceFilePath = location.SourceTree?.FilePath ?? "";
            var sourceLine = lineSpan.StartLinePosition.Line + 1;
            var sourceColumn = lineSpan.StartLinePosition.Character + 1;
            var assembly = symbol.ContainingAssembly?.Name;

            return DefinitionInfo.FromSourceLocation(sourceFilePath, sourceLine, sourceColumn, assembly);
        }
        else
        {
            // Symbol is in metadata (DLL) - decompile it
            var assembly = symbol.ContainingAssembly;
            if (assembly == null)
            {
                logger.LogWarning("Symbol has no containing assembly");
                return null;
            }

            // Get assembly file path from metadata reference
            string? assemblyPath = null;
            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                foreach (var reference in compilation.References)
                {
                    if (reference is Microsoft.CodeAnalysis.PortableExecutableReference peRef)
                    {
                        // Check if this reference matches our assembly
                        var refAssembly = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                        if (refAssembly?.Identity.Equals(assembly.Identity) == true)
                        {
                            assemblyPath = peRef.FilePath;
                            break;
                        }
                    }
                }

                if (assemblyPath != null) break;
            }

            if (assemblyPath.IsNullOrEmpty())
            {
                logger.LogWarning("Could not find assembly path for {Assembly}", assembly.Name);
                return null;
            }

            // Get the full type name for decompilation
            // For nested types or members, we need to decompile the containing type
            var typeToDecompile = symbol as ITypeSymbol ?? symbol.ContainingType;
            if (typeToDecompile == null)
            {
                logger.LogWarning("Symbol {Symbol} has no containing type to decompile", symbol.Name);
                return null;
            }

            var fullTypeName = typeToDecompile.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "")
                .Replace("<", "`")
                .Split('`')[0]; // Remove generic arity notation for decompiler

            var decompiledSource = decompilerService.DecompileType(assemblyPath, fullTypeName);
            if (decompiledSource == null)
            {
                logger.LogWarning("Failed to decompile {TypeName} from {Assembly}", fullTypeName, assemblyPath);
                return null;
            }

            // Try to determine package name from assembly identity
            string? package = null;
            if (assembly.Identity != null && !assembly.Identity.IsRetargetable)
            {
                package = assembly.Name;
            }

            return DefinitionInfo.FromDecompiledSource(decompiledSource, assembly.Name, package);
        }
    }

    public async Task<TypeMembersInfo?> GetTypeMembersAsync(
        string typeName,
        bool includeInherited = false)
    {
        if (workspaceManager.CurrentSolution == null)
        {
            throw new InvalidOperationException("Workspace not initialized");
        }

        ITypeSymbol? typeSymbol = null;

        // Look up the type by fully qualified name
        foreach (var project in workspaceManager.CurrentSolution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            typeSymbol = compilation.GetTypeByMetadataName(typeName);
            if (typeSymbol != null) break;
        }

        if (typeSymbol == null)
        {
            logger.LogWarning("Type {TypeName} not found", typeName);
            return null;
        }

        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (ns == "<global namespace>") ns = null;

        var assembly = typeSymbol.ContainingAssembly;
        var assemblyName = assembly?.Name;

        // Check if type is in workspace source or DLL
        var locations = typeSymbol.Locations;
        if (locations.Length > 0 && locations[0].IsInSource)
        {
            // Type is in workspace - extract source code from syntax tree
            var location = locations[0];
            var syntaxTree = location.SourceTree;
            if (syntaxTree == null)
            {
                logger.LogWarning("Type {TypeName} has no syntax tree", typeName);
                return null;
            }

            var root = await syntaxTree.GetRootAsync();
            var node = root.FindNode(location.SourceSpan);

            // Find the type declaration node (class, struct, interface, enum, record, delegate)
            var typeDeclarationNode = node.AncestorsAndSelf()
                .FirstOrDefault(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax ||
                                     n is Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax ||
                                     n is Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax);

            if (typeDeclarationNode == null)
            {
                logger.LogWarning("Could not find type declaration node for {TypeName}", typeName);
                return null;
            }

            var sourceCode = typeDeclarationNode.ToFullString();
            var filePath = syntaxTree.FilePath;

            return new TypeMembersInfo(
                sourceCode,
                typeSymbol.Name,
                ns,
                assemblyName,
                null,
                true,
                filePath
            );
        }
        else
        {
            // Type is in metadata (DLL) - decompile it
            if (assembly == null)
            {
                logger.LogWarning("Type {TypeName} has no containing assembly", typeName);
                return null;
            }

            // Find assembly file path
            string? assemblyPath = null;
            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                foreach (var reference in compilation.References)
                {
                    if (reference is Microsoft.CodeAnalysis.PortableExecutableReference peRef)
                    {
                        var refAssembly = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                        if (refAssembly?.Identity.Equals(assembly.Identity) == true)
                        {
                            assemblyPath = peRef.FilePath;
                            break;
                        }
                    }
                }

                if (assemblyPath != null) break;
            }

            if (assemblyPath.IsNullOrEmpty())
            {
                logger.LogWarning("Could not find assembly path for {Assembly}", assemblyName);
                return null;
            }

            // Prepare type name for decompiler
            var fullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "")
                .Replace("<", "`")
                .Split('`')[0];

            var decompiledSource = decompilerService.DecompileType(assemblyPath, fullTypeName);
            if (decompiledSource == null)
            {
                logger.LogWarning("Failed to decompile {TypeName} from {Assembly}", fullTypeName, assemblyPath);
                return null;
            }

            // Determine package name
            string? package = null;
            if (assembly.Identity != null && !assembly.Identity.IsRetargetable)
            {
                package = assemblyName;
            }

            return new TypeMembersInfo(
                decompiledSource,
                typeSymbol.Name,
                ns,
                assemblyName,
                package,
                false,
                null
            );
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Workspace;

namespace CSharperMcp.Server.Services;

internal class RoslynService
{
    private readonly WorkspaceManager _workspaceManager;
    private readonly DecompilerService _decompilerService;
    private readonly ILogger<RoslynService> _logger;

    public RoslynService(WorkspaceManager workspaceManager, DecompilerService decompilerService, ILogger<RoslynService> logger)
    {
        _workspaceManager = workspaceManager;
        _decompilerService = decompilerService;
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

    public async Task<Models.SymbolInfo?> GetSymbolInfoAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? symbolName = null)
    {
        if (_workspaceManager.CurrentSolution == null)
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
                _logger.LogWarning("Document not found: {FilePath}", filePath);
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
        else if (!string.IsNullOrEmpty(symbolName))
        {
            foreach (var project in _workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                symbol = compilation.GetTypeByMetadataName(symbolName);
                if (symbol != null) break;
            }
        }

        if (symbol == null) return null;

        return MapSymbolToSymbolInfo(symbol);
    }

    private Document? FindDocument(string filePath)
    {
        if (_workspaceManager.CurrentSolution == null) return null;

        foreach (var project in _workspaceManager.CurrentSolution.Projects)
        {
            var doc = project.Documents.FirstOrDefault(d =>
                d.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) == true);
            if (doc != null) return doc;
        }

        return null;
    }

    private static Models.SymbolInfo MapSymbolToSymbolInfo(ISymbol symbol)
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

        var docComment = symbol.GetDocumentationCommentXml();
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

        string? definitionLocation = null;
        var locations = symbol.Locations;
        if (locations.Length > 0 && locations[0].IsInSource)
        {
            var loc = locations[0];
            var lineSpan = loc.GetLineSpan();
            definitionLocation = $"{loc.SourceTree?.FilePath}:{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}";
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
            definitionLocation
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

    public async Task<IEnumerable<ReferenceInfo>> FindReferencesAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? symbolName = null)
    {
        if (_workspaceManager.CurrentSolution == null)
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
                _logger.LogWarning("Document not found: {FilePath}", filePath);
                return Array.Empty<ReferenceInfo>();
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[line.Value - 1].Start + column.Value - 1;

            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null) return Array.Empty<ReferenceInfo>();

            var syntaxRoot = await document.GetSyntaxRootAsync();
            if (syntaxRoot == null) return Array.Empty<ReferenceInfo>();

            var node = syntaxRoot.FindToken(position).Parent;
            if (node == null) return Array.Empty<ReferenceInfo>();

            var symbolInfo = semanticModel.GetSymbolInfo(node);
            symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node);
        }
        // Get symbol by fully qualified name
        else if (!string.IsNullOrEmpty(symbolName))
        {
            foreach (var project in _workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                symbol = compilation.GetTypeByMetadataName(symbolName);
                if (symbol != null) break;
            }
        }

        if (symbol == null) return Array.Empty<ReferenceInfo>();

        // Find all references
        var references = await SymbolFinder.FindReferencesAsync(
            symbol,
            _workspaceManager.CurrentSolution);

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

                // Get context snippet (the line containing the reference)
                var textLine = sourceText.Lines[lineSpan.StartLinePosition.Line];
                var contextSnippet = textLine.ToString().Trim();

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

        return results;
    }

    public async Task<DefinitionInfo?> GetDefinitionAsync(
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? symbolName = null)
    {
        if (_workspaceManager.CurrentSolution == null)
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
                _logger.LogWarning("Document not found: {FilePath}", filePath);
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
        else if (!string.IsNullOrEmpty(symbolName))
        {
            _logger.LogInformation("Looking up symbol by name: {SymbolName}", symbolName);
            foreach (var project in _workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                symbol = compilation.GetTypeByMetadataName(symbolName);
                if (symbol != null)
                {
                    _logger.LogInformation("Found symbol {SymbolName} in project {ProjectName}", symbolName, project.Name);
                    break;
                }
            }

            if (symbol == null)
            {
                _logger.LogWarning("Symbol {SymbolName} not found in any project", symbolName);
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
                _logger.LogWarning("Symbol has no containing assembly");
                return null;
            }

            // Get assembly file path from metadata reference
            string? assemblyPath = null;
            foreach (var project in _workspaceManager.CurrentSolution.Projects)
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

            if (string.IsNullOrEmpty(assemblyPath))
            {
                _logger.LogWarning("Could not find assembly path for {Assembly}", assembly.Name);
                return null;
            }

            // Get the full type name for decompilation
            // For nested types or members, we need to decompile the containing type
            var typeToDecompile = symbol as ITypeSymbol ?? symbol.ContainingType;
            if (typeToDecompile == null)
            {
                _logger.LogWarning("Symbol {Symbol} has no containing type to decompile", symbol.Name);
                return null;
            }

            var fullTypeName = typeToDecompile.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "")
                .Replace("<", "`")
                .Split('`')[0]; // Remove generic arity notation for decompiler

            var decompiledSource = _decompilerService.DecompileType(assemblyPath, fullTypeName);
            if (decompiledSource == null)
            {
                _logger.LogWarning("Failed to decompile {TypeName} from {Assembly}", fullTypeName, assemblyPath);
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
}

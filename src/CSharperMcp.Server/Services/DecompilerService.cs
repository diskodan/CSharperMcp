using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Workspace;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CSharperMcp.Server.Services;

internal class DecompilerService(WorkspaceManager workspaceManager, ILogger<DecompilerService> logger)
{
    public async Task<DecompiledSourceInfo?> DecompileTypeAsync(
        string typeName,
        string? assemblyName,
        bool includeImplementation = false)
    {
        try
        {
            if (workspaceManager.CurrentSolution == null)
            {
                throw new InvalidOperationException("Workspace not initialized");
            }

            // Find the assembly path for the given type
            string? assemblyPath = null;
            string? actualAssemblyName = null;

            foreach (var project in workspaceManager.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var typeSymbol = compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol != null)
                {
                    var assembly = typeSymbol.ContainingAssembly;
                    actualAssemblyName = assembly?.Name;

                    // Find the assembly file path
                    foreach (var reference in compilation.References)
                    {
                        if (reference is Microsoft.CodeAnalysis.PortableExecutableReference peRef)
                        {
                            var refAssembly = compilation.GetAssemblyOrModuleSymbol(reference) as Microsoft.CodeAnalysis.IAssemblySymbol;
                            if (refAssembly?.Identity.Equals(assembly?.Identity) == true)
                            {
                                assemblyPath = peRef.FilePath;
                                break;
                            }
                        }
                    }

                    if (assemblyPath != null) break;
                }
            }

            if (assemblyPath == null || actualAssemblyName == null)
            {
                logger.LogWarning("Could not find assembly path for type {TypeName}", typeName);
                return null;
            }

            // Prepare type name for decompiler (handle generics)
            var cleanTypeName = typeName.Replace("<", "`").Split('`')[0];

            var decompiledSource = DecompileType(assemblyPath, cleanTypeName, includeImplementation);
            if (decompiledSource == null)
            {
                return null;
            }

            var lineCount = decompiledSource.Split('\n').Length;

            // Extract namespace and simple type name
            var lastDot = typeName.LastIndexOf('.');
            var ns = lastDot > 0 ? typeName.Substring(0, lastDot) : "";
            var simpleTypeName = lastDot > 0 ? typeName.Substring(lastDot + 1) : typeName;

            // Determine if this is a NuGet package (not BCL)
            string? package = null;
            if (actualAssemblyName != null &&
                !actualAssemblyName.StartsWith("System") &&
                !actualAssemblyName.StartsWith("Microsoft."))
            {
                package = actualAssemblyName;
            }

            // Detect obfuscation
            var isObfuscated = IsLikelyObfuscated(decompiledSource);
            string? obfuscationWarning = isObfuscated
                ? "This assembly appears to be obfuscated. Decompiled source may contain unreadable identifiers and may not accurately represent the original code structure."
                : null;

            return new DecompiledSourceInfo(
                TypeName: simpleTypeName,
                Namespace: ns,
                Assembly: actualAssemblyName ?? "Unknown",
                Package: package,
                DecompiledSource: decompiledSource,
                IncludesImplementation: includeImplementation,
                LineCount: lineCount,
                IsLikelyObfuscated: isObfuscated,
                ObfuscationWarning: obfuscationWarning
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decompile type {TypeName}", typeName);
            return null;
        }
    }

    public string? DecompileType(string assemblyPath, string fullTypeName, bool includeImplementation = true)
    {
        try
        {
            var settings = new DecompilerSettings
            {
                ThrowOnAssemblyResolveErrors = false,
                DecompileMemberBodies = includeImplementation,  // Use built-in ICSharpCode.Decompiler setting
                ShowXmlDocumentation = true  // Always include XML documentation
            };

            var decompiler = new CSharpDecompiler(assemblyPath, settings);

            var typeName = new FullTypeName(fullTypeName);
            var decompiledSource = decompiler.DecompileTypeAsString(typeName);

            return decompiledSource;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decompile type {TypeName} from {Assembly}", fullTypeName, assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Detects whether decompiled source code appears to be obfuscated using heuristic analysis.
    /// Returns true if the code contains patterns commonly found in obfuscated assemblies.
    /// </summary>
    /// <param name="decompiledSource">The decompiled C# source code to analyze</param>
    /// <returns>True if obfuscation is likely detected, false otherwise</returns>
    public bool IsLikelyObfuscated(string decompiledSource)
    {
        if (string.IsNullOrWhiteSpace(decompiledSource))
        {
            return false;
        }

        var obfuscationScore = 0;

        // Heuristic 1: Single-character identifiers (excluding common loop variables i, j, k in for loops)
        // Look for single-char type names, method names, or field names
        var singleCharIdentifierPattern = new Regex(@"\b(class|struct|interface|enum|delegate)\s+[a-zA-Z]\b|\b(public|private|protected|internal|static|readonly)\s+\w+\s+[a-zA-Z]\b");
        var singleCharMatches = singleCharIdentifierPattern.Matches(decompiledSource);
        if (singleCharMatches.Count > 5)
        {
            obfuscationScore += 3;
        }

        // Heuristic 2: Obfuscator naming patterns (e.g., "A00001", "c__01", "CS$<>")
        // Patterns: A00001 (capital + 5+ digits), c__01 (lowercase + __ + digits), compiler-generated patterns
        var obfuscatorPatternRegex = new Regex(@"[A-Z]\d{5,}|[a-z]__\d+|CS\$<>|<>c__|<>f__|<PrivateImplementationDetails>");
        var obfuscatorMatches = obfuscatorPatternRegex.Matches(decompiledSource);
        if (obfuscatorMatches.Count > 5)
        {
            obfuscationScore += 4;
        }
        else if (obfuscatorMatches.Count > 2)
        {
            obfuscationScore += 2;
        }

        // Heuristic 3: Excessive unicode escape sequences (not in string literals)
        // Remove string literals first to avoid false positives
        var codeWithoutStrings = Regex.Replace(decompiledSource, @"""(?:[^""\\]|\\.)*""", "");
        var unicodeEscapeCount = Regex.Matches(codeWithoutStrings, @"\\u[0-9a-fA-F]{4}").Count;
        if (unicodeEscapeCount > 10)
        {
            obfuscationScore += 4;
        }
        else if (unicodeEscapeCount > 5)
        {
            obfuscationScore += 2;
        }

        // Heuristic 4: Very short average identifier length
        // Extract identifiers (exclude keywords)
        var identifierPattern = new Regex(@"\b(?!class|struct|interface|enum|public|private|protected|internal|static|readonly|void|int|string|bool|if|else|for|while|return|new|using|namespace|get|set|value)\w+\b");
        var identifiers = identifierPattern.Matches(decompiledSource);
        if (identifiers.Count > 0)
        {
            var totalLength = identifiers.Sum(m => m.Value.Length);
            var averageLength = (double)totalLength / identifiers.Count;
            if (averageLength < 3.0)
            {
                obfuscationScore += 3;
            }
            else if (averageLength < 4.5)
            {
                obfuscationScore += 1;
            }
        }

        // Heuristic 5: High ratio of single-letter method parameters
        var methodParameterPattern = new Regex(@"\([^)]*\b[a-zA-Z]\b[^)]*\)");
        var methodParameterMatches = methodParameterPattern.Matches(decompiledSource);
        if (methodParameterMatches.Count > 15)
        {
            obfuscationScore += 2;
        }

        // Threshold: Score >= 5 suggests likely obfuscation
        return obfuscationScore >= 5;
    }

}

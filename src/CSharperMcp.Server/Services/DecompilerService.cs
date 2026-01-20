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

            // Type name should already be in metadata format (e.g., "List`1" not "List<T>")
            // If it contains angle brackets, convert to metadata format
            var decompilerTypeName = typeName;
            if (typeName.Contains('<'))
            {
                // Convert from "List<T>" to "List`1" format
                var angleBracketIndex = typeName.IndexOf('<');
                var genericParams = typeName.Substring(angleBracketIndex + 1, typeName.LastIndexOf('>') - angleBracketIndex - 1)
                    .Split(',').Length;
                decompilerTypeName = typeName.Substring(0, angleBracketIndex) + "`" + genericParams;
            }

            var decompiledSource = DecompileType(assemblyPath, decompilerTypeName, includeImplementation);
            if (decompiledSource == null)
            {
                return null;
            }

            var lineCount = decompiledSource.Split('\n').Length;

            // Extract namespace and simple type name
            var lastDot = typeName.LastIndexOf('.');
            var ns = lastDot > 0 ? typeName.Substring(0, lastDot) : "";
            var simpleTypeName = lastDot > 0 ? typeName.Substring(lastDot + 1) : typeName;

            // Remove generic arity marker (e.g., "List`1" -> "List", "Dictionary`2" -> "Dictionary")
            var backtickIndex = simpleTypeName.IndexOf('`');
            if (backtickIndex > 0)
            {
                simpleTypeName = simpleTypeName.Substring(0, backtickIndex);
            }

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
                DecompileMemberBodies = includeImplementation,
                ShowXmlDocumentation = includeImplementation,  // Only show XML docs for full implementation
                RemoveDeadCode = false,
                AlwaysUseBraces = false
            };

            var decompiler = new CSharpDecompiler(assemblyPath, settings);

            var typeName = new FullTypeName(fullTypeName);
            var decompiledSource = decompiler.DecompileTypeAsString(typeName);

            // For signatures-only mode, post-process to create truly compact output
            if (!includeImplementation)
            {
                decompiledSource = CreateSignaturesOnlyOutput(decompiledSource);
            }

            return decompiledSource;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decompile type {TypeName} from {Assembly}", fullTypeName, assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Post-processes decompiled source to create signatures-only output by:
    /// 1. Removing method/property bodies (replace with ; for methods, keep auto-property syntax)
    /// 2. Removing empty lines and excessive whitespace
    /// 3. Keeping only declarations, attributes, and structural information
    /// </summary>
    private string CreateSignaturesOnlyOutput(string decompiledSource)
    {
        var lines = decompiledSource.Split('\n');
        var result = new System.Text.StringBuilder();
        var braceDepth = 0;
        var skipUntilCloseBrace = 0;
        var lastLineWasEmpty = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Skip empty lines to reduce output
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (!lastLineWasEmpty && braceDepth > 0)
                {
                    result.AppendLine();
                    lastLineWasEmpty = true;
                }
                continue;
            }
            lastLineWasEmpty = false;

            // Count braces to track nesting depth
            var openBraces = trimmed.Count(c => c == '{');
            var closeBraces = trimmed.Count(c => c == '}');

            // Check if we're entering a method, property, or constructor body
            if (skipUntilCloseBrace == 0)
            {
                // Detect method/constructor/property declarations (but not type declarations)
                var isMethodLike = (trimmed.Contains('(') && trimmed.Contains(')')) || // Method or constructor
                                   (trimmed.Contains('{') && (trimmed.Contains(" get") || trimmed.Contains(" set") || trimmed.Contains(" init"))); // Property with body

                var isTypeDeclaration = trimmed.Contains("class ") || trimmed.Contains("struct ") ||
                                       trimmed.Contains("interface ") || trimmed.Contains("enum ") ||
                                       trimmed.Contains("namespace ");

                if (isMethodLike && !isTypeDeclaration && openBraces > 0)
                {
                    // This is a method/property with a body - replace body with ;
                    // Output the line up to and including the opening brace, then close it
                    var braceIndex = line.IndexOf('{');
                    if (braceIndex >= 0)
                    {
                        // For auto-properties like "public int X { get; set; }", keep as-is
                        if (trimmed.EndsWith("{ get; set; }") || trimmed.EndsWith("{ get; init; }") ||
                            trimmed.EndsWith("{ get; }") || trimmed.EndsWith("{ set; }"))
                        {
                            result.AppendLine(line);
                        }
                        else
                        {
                            // Replace method body with throw statement or empty block
                            var declaration = line.Substring(0, braceIndex).TrimEnd();
                            result.AppendLine(declaration + ";");
                            skipUntilCloseBrace = 1; // Skip until we close this brace
                        }
                    }
                    else
                    {
                        result.AppendLine(line);
                    }
                }
                else
                {
                    // Regular line (attribute, type declaration, field, etc.)
                    result.AppendLine(line);
                }
            }
            else
            {
                // We're inside a method body we want to skip
                skipUntilCloseBrace += openBraces - closeBraces;
                if (skipUntilCloseBrace <= 0)
                {
                    skipUntilCloseBrace = 0; // Reset when we close all braces
                }
                // Skip this line entirely
                continue;
            }

            braceDepth += openBraces - closeBraces;
        }

        return result.ToString();
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

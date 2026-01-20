using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Workspace;
using Microsoft.Extensions.Logging;

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

            return new DecompiledSourceInfo(
                TypeName: simpleTypeName,
                Namespace: ns,
                Assembly: actualAssemblyName ?? "Unknown",
                Package: package,
                DecompiledSource: decompiledSource,
                IncludesImplementation: includeImplementation,
                LineCount: lineCount
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

}

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.Services;

internal class DecompilerService
{
    private readonly ILogger<DecompilerService> _logger;

    public DecompilerService(ILogger<DecompilerService> logger)
    {
        _logger = logger;
    }

    public string? DecompileType(string assemblyPath, string fullTypeName)
    {
        try
        {
            var decompiler = new CSharpDecompiler(assemblyPath, new DecompilerSettings
            {
                ThrowOnAssemblyResolveErrors = false
            });

            var typeName = new FullTypeName(fullTypeName);
            var decompiledSource = decompiler.DecompileTypeAsString(typeName);

            return decompiledSource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decompile type {TypeName} from {Assembly}", fullTypeName, assemblyPath);
            return null;
        }
    }
}

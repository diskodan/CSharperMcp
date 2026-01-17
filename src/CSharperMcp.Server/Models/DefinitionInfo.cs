namespace CSharperMcp.Server.Models;

/// <summary>
/// Represents the definition location of a symbol.
/// Either contains source location (FilePath/Line/Column) for workspace symbols,
/// or decompiled source (DecompiledSource) for DLL/metadata symbols.
/// </summary>
internal record DefinitionInfo(
    /// <summary>File path for workspace symbols, null for DLL symbols</summary>
    string? FilePath,

    /// <summary>1-based line number for workspace symbols, null for DLL symbols</summary>
    int? Line,

    /// <summary>1-based column number for workspace symbols, null for DLL symbols</summary>
    int? Column,

    /// <summary>Decompiled C# source code for DLL symbols, null for workspace symbols</summary>
    string? DecompiledSource,

    /// <summary>Assembly name containing the symbol</summary>
    string? Assembly,

    /// <summary>NuGet package name if applicable</summary>
    string? Package,

    /// <summary>Whether this is a source location (true) or decompiled (false)</summary>
    bool IsSourceLocation
)
{
    /// <summary>Creates a DefinitionInfo for a workspace symbol with source location</summary>
    public static DefinitionInfo FromSourceLocation(string filePath, int line, int column, string? assembly = null)
        => new(filePath, line, column, null, assembly, null, true);

    /// <summary>Creates a DefinitionInfo for a DLL symbol with decompiled source</summary>
    public static DefinitionInfo FromDecompiledSource(string decompiledSource, string assembly, string? package = null)
        => new(null, null, null, decompiledSource, assembly, package, false);
}

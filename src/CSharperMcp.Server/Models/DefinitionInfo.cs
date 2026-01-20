namespace CSharperMcp.Server.Models;

/// <summary>
/// Represents the definition location of a symbol.
/// For workspace symbols: contains source location (FilePath/Line/Column).
/// For DLL symbols: contains metadata (Assembly/TypeName/SymbolKind/Signature/Package).
/// </summary>
/// <param name="IsFromWorkspace">Whether the symbol is from the workspace (true) or from a DLL (false)</param>
/// <param name="FilePath">File path for workspace symbols, null for DLL symbols</param>
/// <param name="Line">1-based line number for workspace symbols, null for DLL symbols</param>
/// <param name="Column">1-based column number for workspace symbols, null for DLL symbols</param>
/// <param name="Assembly">Assembly name containing the symbol</param>
/// <param name="TypeName">Fully qualified type name for DLL symbols, null for workspace symbols</param>
/// <param name="SymbolKind">Symbol kind for DLL symbols (Class, Interface, Method, etc.), null for workspace symbols</param>
/// <param name="Signature">Brief signature for DLL symbols, null for workspace symbols</param>
/// <param name="Package">NuGet package name for DLL symbols if applicable, null for workspace symbols</param>
internal record DefinitionInfo(
    bool IsFromWorkspace,
    string? FilePath,
    int? Line,
    int? Column,
    string Assembly,
    string? TypeName,
    string? SymbolKind,
    string? Signature,
    string? Package
);

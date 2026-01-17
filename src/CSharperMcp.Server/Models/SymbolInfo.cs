namespace CSharperMcp.Server.Models;

internal record SymbolInfo(
    string Kind,
    string Name,
    string? ContainingType,
    string? Namespace,
    string? Assembly,
    string? Package,
    string? DocComment,
    IReadOnlyList<string> Modifiers,
    string? Signature,
    string? DefinitionLocation
);

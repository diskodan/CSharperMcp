namespace CSharperMcp.Server.Models;

internal record ReferenceInfo(
    string FilePath,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    string ContextSnippet,
    string ReferenceKind
);

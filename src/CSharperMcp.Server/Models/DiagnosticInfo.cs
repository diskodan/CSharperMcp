namespace CSharperMcp.Server.Models;

internal record DiagnosticInfo(
    string Id,
    string Message,
    string Severity,
    string? File,
    int? Line,
    int? Column,
    int? EndLine,
    int? EndColumn,
    string Category,
    bool HasFix
);

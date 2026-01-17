namespace CSharperMcp.Server.Models;

/// <summary>
/// Represents a code action (fix or refactoring) available at a location.
/// </summary>
internal record CodeActionInfo(
    /// <summary>Unique identifier for this action (can be used with apply_code_action)</summary>
    string Id,

    /// <summary>Human-readable title describing the action</summary>
    string Title,

    /// <summary>Kind of action (e.g., "QuickFix", "Refactor")</summary>
    string Kind,

    /// <summary>Diagnostic IDs this action fixes (empty for refactorings)</summary>
    IReadOnlyList<string> DiagnosticIds
);

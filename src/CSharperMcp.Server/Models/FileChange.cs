namespace CSharperMcp.Server.Models;

/// <summary>
/// Represents a change to a single file
/// </summary>
internal record FileChange(
    /// <summary>Absolute path to the changed file</summary>
    string FilePath,

    /// <summary>Original content before the change (for preview mode)</summary>
    string? OriginalContent,

    /// <summary>Modified content after the change (for preview mode)</summary>
    string? ModifiedContent,

    /// <summary>Unified diff format (for preview mode)</summary>
    string? UnifiedDiff
);

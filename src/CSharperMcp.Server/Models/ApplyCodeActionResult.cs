namespace CSharperMcp.Server.Models;

/// <summary>
/// Result of applying a code action
/// </summary>
internal record ApplyCodeActionResult(
    /// <summary>Whether the action was successfully applied</summary>
    bool Success,

    /// <summary>Error message if the action failed</summary>
    string? ErrorMessage,

    /// <summary>List of file changes made by this action</summary>
    IReadOnlyList<FileChange> Changes
);

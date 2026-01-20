namespace CSharperMcp.Server.Models;

/// <summary>
/// Result of a GetCodeActions request with pagination support.
/// </summary>
internal record CodeActionsResult(
    /// <summary>List of code actions in the current page</summary>
    IReadOnlyList<CodeActionInfo> Actions,

    /// <summary>Total count of all available code actions (before pagination)</summary>
    int TotalCount,

    /// <summary>Actual count of actions returned in this response</summary>
    int ReturnedCount,

    /// <summary>Whether there are more results available beyond this page</summary>
    bool HasMore
);

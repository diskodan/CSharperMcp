namespace CSharperMcp.Server.Models;

internal record FindReferencesResult(
    int TotalCount,
    bool HasMore,
    IReadOnlyList<ReferenceInfo> References
);

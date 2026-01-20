namespace CSharperMcp.Server.Models;

internal record FindSymbolUsagesResult(
    int TotalCount,
    bool HasMore,
    IReadOnlyList<ReferenceInfo> Usages
);

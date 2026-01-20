namespace CSharperMcp.Server.Models;

/// <summary>
/// Represents the full definition of a type with all its members.
/// Contains the complete source code (either from workspace or decompiled).
/// </summary>
internal record TypeMembersInfo(
    /// <summary>Full C# source code for the type</summary>
    string SourceCode,

    /// <summary>Type name</summary>
    string TypeName,

    /// <summary>Namespace containing the type</summary>
    string? Namespace,

    /// <summary>Assembly containing the type</summary>
    string? Assembly,

    /// <summary>NuGet package name if applicable</summary>
    string? Package,

    /// <summary>Whether this is workspace source (true) or decompiled (false)</summary>
    bool IsFromWorkspace,

    /// <summary>File path if from workspace, null if decompiled</summary>
    string? FilePath,

    /// <summary>Whether the source code includes implementation details or just signatures</summary>
    bool IncludesImplementation,

    /// <summary>Number of lines in the source code (for size estimation)</summary>
    int LineCount
);

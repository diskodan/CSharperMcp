namespace CSharperMcp.Server.Models;

/// <summary>
/// Represents decompiled source code for a type from a DLL (BCL, NuGet package, etc.).
/// Can contain either full source with implementation or signatures-only (reference assembly style).
/// </summary>
internal record DecompiledSourceInfo(
    /// <summary>Simple type name (without namespace)</summary>
    string TypeName,

    /// <summary>Namespace containing the type</summary>
    string Namespace,

    /// <summary>Assembly containing the type (includes version info)</summary>
    string Assembly,

    /// <summary>NuGet package name if applicable, null for BCL types</summary>
    string? Package,

    /// <summary>Decompiled C# source code</summary>
    string DecompiledSource,

    /// <summary>Whether method bodies are included (true) or signatures only (false)</summary>
    bool IncludesImplementation,

    /// <summary>Number of lines in the decompiled source (for LLM context window planning)</summary>
    int LineCount
);

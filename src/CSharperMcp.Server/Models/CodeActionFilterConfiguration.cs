using Microsoft.CodeAnalysis;

namespace CSharperMcp.Server.Models;

/// <summary>
/// Configuration for filtering code actions to reduce noise and improve LLM usability.
/// </summary>
internal record CodeActionFilterConfiguration
{
    /// <summary>
    /// Maximum number of code actions to return per request. Default: 50.
    /// </summary>
    public int MaxResults { get; init; } = 50;

    /// <summary>
    /// Diagnostic IDs to exclude (e.g., noisy style suggestions).
    /// </summary>
    public HashSet<string> ExcludedDiagnosticIds { get; init; } = new()
    {
        // Style preferences that are often subjective or too granular
        "IDE0001", // Simplify name (often noisy)
        "IDE0002", // Simplify member access (often noisy)
        "IDE0004", // Remove unnecessary cast (often obvious)
        "IDE0010", // Add missing cases to switch (often not helpful)
        "IDE0055", // Formatting violations (too granular)
        "IDE0071", // Simplify interpolation (stylistic)
        "IDE0072", // Add missing cases to switch expression (often not helpful)
        "IDE0082", // Convert typeof to nameof (stylistic)
    };

    /// <summary>
    /// Refactoring titles to exclude (pattern matching).
    /// </summary>
    public HashSet<string> ExcludedRefactoringPatterns { get; init; } = new()
    {
        "Sort usings", // Often noisy
        "Remove unused usings", // Handled by IDE0005 diagnostic
        "Organize usings", // Often noisy
        "Add file banner", // Too opinionated
        "Generate XML documentation comment", // Let the developer decide
    };

    /// <summary>
    /// Whether to include refactorings at all. Default: true.
    /// If false, only code fixes (diagnostic-based) will be returned.
    /// </summary>
    public bool IncludeRefactorings { get; init; } = true;

    /// <summary>
    /// Priority-boosted diagnostic IDs (important fixes that should always be shown).
    /// These bypass the ExcludedDiagnosticIds filter.
    /// </summary>
    public HashSet<string> PriorityDiagnosticIds { get; init; } = new()
    {
        // Compiler errors
        "CS0103", // Name does not exist
        "CS0246", // Type or namespace not found
        "CS1061", // Type does not contain definition
        "CS0029", // Cannot implicitly convert

        // Important code quality issues
        "CS0168", // Variable declared but never used
        "CS0219", // Variable assigned but never used
        "CA1031", // Do not catch general exception types
        "CA2007", // Do not directly await Task
    };

    /// <summary>
    /// Whether to deduplicate actions with the same title. Default: true.
    /// Multiple providers may suggest the same action.
    /// </summary>
    public bool DeduplicateByTitle { get; init; } = true;
}

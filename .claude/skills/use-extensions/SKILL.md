---
name: use-extensions
descriptoin: prefer helpful extension methods
---

# Use Extension Methods Skill

This skill helps identify opportunities to use the project's custom extension methods for cleaner, more idiomatic code.

## Extension Methods Available

### StringExtensions (`CSharperMcp.Server.Extensions`)

- **`IsNullOrEmpty()`** - Extension method wrapper for `string.IsNullOrEmpty(value)`
  - Use: `myString.IsNullOrEmpty()` instead of `string.IsNullOrEmpty(myString)`

- **`IsNullOrWhiteSpace()`** - Extension method wrapper for `string.IsNullOrWhiteSpace(value)`
  - Use: `myString.IsNullOrWhiteSpace()` instead of `string.IsNullOrWhiteSpace(myString)`

### CollectionExtensions (`CSharperMcp.Server.Extensions`)

- **`OrEmpty<T>()`** - Returns the collection or an empty collection if null
  - Works with: `IEnumerable<T>`, `List<T>`, `T[]`, `IReadOnlyList<T>`
  - Use: `nullableList.OrEmpty().Where(...)` instead of `(nullableList ?? []).Where(...)`
  - Enables fluent null-safe chaining without the `?.` operator

## When to Use This Skill

Invoke this skill when:

- Reviewing code that has verbose null checks on strings or collections
- Refactoring code to be more fluent and readable
- Writing new code that needs null-safe collection operations
- Seeing patterns like `(collection ?? [])` or `string.IsNullOrEmpty(variable)`

## Instructions

When this skill is invoked:

1. **Search for opportunities**: Look for these patterns in the codebase:
   - `string.IsNullOrEmpty(variable)` → Replace with `variable.IsNullOrEmpty()`
   - `string.IsNullOrWhiteSpace(variable)` → Replace with `variable.IsNullOrWhiteSpace()`
   - `(collection ?? [])` → Replace with `collection.OrEmpty()`
   - `collection?.Where(...)` or `collection?.Select(...)` where null results in no action → Consider `collection.OrEmpty().Where(...)`

2. **Apply transformations**:
   - Add using directive if needed: `using CSharperMcp.Server.Extensions;`
   - Replace static method calls with extension method calls
   - Replace null-coalescing patterns with `.OrEmpty()` calls
   - Ensure the code remains semantically equivalent

3. **Verify namespace**:
   - These extension methods are in the `CSharperMcp.Server.Extensions` namespace
   - They are `internal static` classes, accessible within the `CSharperMcp.Server` project and test projects

4. **Report changes**: Provide a summary of:
   - Number of occurrences found
   - Files modified
   - Specific transformations applied

## Examples

### Before (String)

```csharp
if (string.IsNullOrEmpty(fileName))
{
    return;
}

if (string.IsNullOrWhiteSpace(userInput))
{
    throw new ArgumentException("Input cannot be empty");
}
```

### After (String)

```csharp
using CSharperMcp.Server.Extensions;

if (fileName.IsNullOrEmpty())
{
    return;
}

if (userInput.IsNullOrWhiteSpace())
{
    throw new ArgumentException("Input cannot be empty");
}
```

### Before (Collections)

```csharp
var items = nullableList ?? [];
foreach (var item in items)
{
    Process(item);
}

var filtered = (diagnostics ?? []).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
```

### After (Collections)

```csharp
using CSharperMcp.Server.Extensions;

var items = nullableList.OrEmpty();
foreach (var item in items)
{
    Process(item);
}

var filtered = diagnostics.OrEmpty().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
```

## Constraints

- Only use these extension methods within the `CSharperMcp.Server` project and its test projects
- Do not modify the extension method implementations themselves unless explicitly requested
- Maintain semantic equivalence - ensure the behavior doesn't change
- These are utility methods; use them where they improve readability, not everywhere possible

## Usage

Invoke this skill with:

```
/use-extensions
```

Or with a specific scope:

```
/use-extensions src/CSharperMcp.Server/Services/
```

# CSharper MCP Server - Usability Review

Generated: 2026-01-19

This document analyzes how the MCP server appears to clients (Claude Code, Cursor, etc.) and identifies potential usability issues.

---

## Server Information

### Server Capabilities

**Name:** CSharper MCP Server
**Version:** 1.0.0
**Protocol Version:** 2024-11-05

**Server Instructions (sent to clients):**

```
This MCP server provides semantic C# language server capabilities for LLMs.

Features:
- Workspace initialization from .sln or .csproj files
- Compiler diagnostics (errors, warnings, analyzer messages)
- Symbol resolution and navigation (including NuGet packages and DLLs)
- Find references across workspace
- Code actions and refactorings
- Decompilation of types from referenced assemblies

Use this server to gain IDE-like understanding of C# codebases without grepping files.
```

**Capabilities:**
- ✅ Tools (8 tools available)
- ❌ Resources
- ❌ Prompts
- ❌ Sampling

---

## Tools Overview

The server exposes 8 tools:

1. `initialize_workspace` - Load solution/project
2. `get_diagnostics` - Get compiler errors/warnings
3. `get_symbol_info` - Get symbol information
4. `find_references` - Find all usages of a symbol
5. `get_definition` - Go to definition
6. `get_type_members` - Get full type definition
7. `get_code_actions` - Get available fixes/refactorings
8. `apply_code_action` - Apply a code action

---

## Tool-by-Tool Analysis

### 1. `initialize_workspace`

**Description:**
"Initialize the C# workspace with a solution or project path. Call this before using other tools."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "path": {
      "type": "string",
      "description": "Absolute path to .sln, .csproj, or directory containing them"
    }
  },
  "required": ["path"]
}
```

**Example Input:**

```json
{
  "path": "/Users/daniel/src/MyProject"
}
```

**Example Output:**

```json
{
  "Success": true,
  "Message": "Loaded solution: MyProject.sln with 3 project(s)",
  "ProjectCount": 3
}
```

**Usability Analysis:**

✅ **Good:**
- Clear, simple parameter
- Success/failure clearly indicated
- Provides useful feedback (project count, which file was loaded)

⚠️ **Potential Issues:**
- No indication of which specific .sln or .csproj was chosen when given a directory
- No way to list what was found before choosing
- Error messages might not provide actionable guidance

**Recommendation:**
- Consider adding `discoveredSolutions` or `discoveredProjects` to the response
- Add `reason` field explaining why a particular solution was chosen

---

### 2. `get_diagnostics`

**Description:**
"Get compiler diagnostics (errors, warnings) for the workspace, a specific file, or a line range. Call initialize_workspace first."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "file": {
      "type": "string",
      "description": "Optional file path to filter diagnostics to a specific file"
    },
    "startLine": {
      "type": "integer",
      "description": "Optional start line (1-based) to filter diagnostics"
    },
    "endLine": {
      "type": "integer",
      "description": "Optional end line (1-based) to filter diagnostics"
    },
    "severity": {
      "type": "string",
      "description": "Minimum severity: error, warning, info, or hidden",
      "default": "warning"
    }
  }
}
```

**Example Input #1 - Entire workspace:**

```json
{}
```

**Example Output #1:**

```json
{
  "Success": true,
  "TotalCount": 42,
  "Diagnostics": [
    {
      "Id": "CS0103",
      "Message": "The name 'undefinedVariable' does not exist in the current context",
      "Severity": "Error",
      "File": "/Users/daniel/src/MyProject/Program.cs",
      "Line": 15,
      "Column": 13,
      "EndLine": 15,
      "EndColumn": 30,
      "Category": "Compiler",
      "HasFix": false
    },
    {
      "Id": "CS0246",
      "Message": "The type or namespace name 'JsonConvert' could not be found (are you missing a using directive or an assembly reference?)",
      "Severity": "Error",
      "File": "/Users/daniel/src/MyProject/Services/JsonService.cs",
      "Line": 42,
      "Column": 17,
      "EndLine": 42,
      "EndColumn": 28,
      "Category": "Compiler",
      "HasFix": false
    },
    {
      "Id": "IDE0005",
      "Message": "Using directive is unnecessary.",
      "Severity": "Warning",
      "File": "/Users/daniel/src/MyProject/Models/User.cs",
      "Line": 3,
      "Column": 1,
      "EndLine": 3,
      "EndColumn": 20,
      "Category": "Style",
      "HasFix": false
    }
  ]
}
```

**Example Input #2 - Specific file, errors only:**

```json
{
  "file": "/Users/daniel/src/MyProject/Program.cs",
  "severity": "error"
}
```

**Example Output #2:**

```json
{
  "Success": true,
  "TotalCount": 1,
  "Diagnostics": [
    {
      "Id": "CS0103",
      "Message": "The name 'undefinedVariable' does not exist in the current context",
      "Severity": "Error",
      "File": "/Users/daniel/src/MyProject/Program.cs",
      "Line": 15,
      "Column": 13,
      "EndLine": 15,
      "EndColumn": 30,
      "Category": "Compiler",
      "HasFix": false
    }
  ]
}
```

**Usability Analysis:**

✅ **Good:**
- Flexible filtering (file, line range, severity)
- Clear structure with all relevant location info
- Category field helps distinguish compiler vs analyzer diagnostics
- `TotalCount` provides quick summary

⚠️ **Potential Issues:**
- `HasFix` is always `false` (TODO in code) - misleading or useless until implemented
- For large workspaces, entire workspace query could return hundreds/thousands of diagnostics
- No pagination or limit parameter
- No way to get diagnostics for a specific project (only file or entire workspace)

**Recommendations:**
- Remove `HasFix` field or implement it properly
- Add `maxResults` parameter with default limit (e.g., 100)
- Add `project` filter parameter
- Consider adding `offset` for pagination
- Consider adding summary counts by severity before the diagnostic list

---

### 3. `get_symbol_info`

**Description:**
"Get symbol information at a specific location or by fully qualified name. Returns type, namespace, assembly, documentation, and signature."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "file": {
      "type": "string",
      "description": "File path to get symbol from (for location-based lookup)"
    },
    "line": {
      "type": "integer",
      "description": "Line number (1-based) for location-based lookup"
    },
    "column": {
      "type": "integer",
      "description": "Column number (1-based) for location-based lookup"
    },
    "symbolName": {
      "type": "string",
      "description": "Fully qualified symbol name for name-based lookup (e.g. 'System.String' or 'Newtonsoft.Json.Linq.JObject')"
    }
  }
}
```

**Example Input #1 - By location:**

```json
{
  "file": "/Users/daniel/src/MyProject/Program.cs",
  "line": 15,
  "column": 13
}
```

**Example Output #1:**

```json
{
  "success": true,
  "symbol": {
    "Kind": "Method",
    "Name": "WriteLine",
    "ContainingType": "Console",
    "Namespace": "System",
    "Assembly": "System.Console, Version=10.0.0.0",
    "Package": null,
    "DocComment": "Writes the current line terminator to the standard output stream.",
    "Modifiers": ["public", "static"],
    "Signature": "void Console.WriteLine(string value)",
    "DefinitionLocation": null
  }
}
```

**Example Input #2 - By fully qualified name:**

```json
{
  "symbolName": "Newtonsoft.Json.Linq.JObject"
}
```

**Example Output #2:**

```json
{
  "success": true,
  "symbol": {
    "Kind": "Class",
    "Name": "JObject",
    "ContainingType": null,
    "Namespace": "Newtonsoft.Json.Linq",
    "Assembly": "Newtonsoft.Json, Version=13.0.0.0",
    "Package": "Newtonsoft.Json",
    "DocComment": "Represents a JSON object.",
    "Modifiers": ["public"],
    "Signature": "public class JObject : JContainer, IDictionary<string, JToken>, ICollection<KeyValuePair<string, JToken>>, IEnumerable<KeyValuePair<string, JToken>>, IEnumerable, INotifyPropertyChanged, ICustomTypeDescriptor, INotifyPropertyChanging",
    "DefinitionLocation": null
  }
}
```

**Usability Analysis:**

✅ **Good:**
- Dual lookup modes (location vs name) is very flexible
- Returns comprehensive symbol information
- Includes documentation
- Package field distinguishes NuGet packages from BCL

⚠️ **Potential Issues:**
- `DefinitionLocation` is always null (unused field?)
- Signature can be extremely long for complex types (see JObject example)
- No indication of whether this is a workspace symbol vs DLL symbol
- DocComment could be very long for some symbols
- No way to request "brief" vs "detailed" info

**Recommendations:**
- Remove `DefinitionLocation` field or document its purpose
- Add `IsFromWorkspace` boolean field
- Consider truncating signatures at a reasonable length with ellipsis
- Consider separate `DocSummary` (brief) vs `DocComment` (full XML doc)
- Consider adding `SourceFile` and `SourceLine` for workspace symbols

---

### 4. `find_references`

**Description:**
"Find all references to a symbol across the workspace. Returns file locations with line numbers and code snippets."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "file": {
      "type": "string",
      "description": "File path to get symbol from (for location-based lookup)"
    },
    "line": {
      "type": "integer",
      "description": "Line number (1-based) for location-based lookup"
    },
    "column": {
      "type": "integer",
      "description": "Column number (1-based) for location-based lookup"
    },
    "symbolName": {
      "type": "string",
      "description": "Fully qualified symbol name for name-based lookup (e.g. 'System.String')"
    }
  }
}
```

**Example Input:**

```json
{
  "file": "/Users/daniel/src/MyProject/Models/User.cs",
  "line": 8,
  "column": 18
}
```

**Example Output:**

```json
{
  "success": true,
  "count": 15,
  "references": [
    {
      "FilePath": "/Users/daniel/src/MyProject/Controllers/UserController.cs",
      "Line": 23,
      "Column": 16,
      "EndLine": 23,
      "EndColumn": 20,
      "ContextSnippet": "        var user = new User { Name = \"John\" };",
      "ReferenceKind": "ObjectCreation"
    },
    {
      "FilePath": "/Users/daniel/src/MyProject/Services/UserService.cs",
      "Line": 42,
      "Column": 9,
      "EndLine": 42,
      "EndColumn": 13,
      "ContextSnippet": "        User existingUser = await _repository.GetAsync(id);",
      "ReferenceKind": "TypeReference"
    },
    {
      "FilePath": "/Users/daniel/src/MyProject/Services/UserService.cs",
      "Line": 57,
      "Column": 34,
      "EndLine": 57,
      "EndColumn": 38,
      "ContextSnippet": "    public async Task<List<User>> GetAllUsersAsync()",
      "ReferenceKind": "TypeReference"
    }
  ]
}
```

**Usability Analysis:**

✅ **Good:**
- Context snippets are very helpful for understanding usage
- `ReferenceKind` provides semantic information
- Includes full location info (start/end line/column)
- `count` provides quick summary

⚠️ **Potential Issues:**
- For popular symbols (e.g., `string`, `int`), could return thousands of references
- No pagination or limiting
- Context snippet is a single line - might not be enough context for complex expressions
- References in DLLs/metadata are likely excluded (not clear from description)

**Recommendations:**
- Add `maxResults` parameter with sensible default (e.g., 100)
- Add `offset` for pagination
- Consider expanding context snippet to 3 lines (before/current/after)
- Clarify in description whether DLL references are included
- Consider grouping by file to reduce verbosity

---

### 5. `get_definition`

**Description:**
"Go to the definition of a symbol. Returns source file location for workspace symbols, or decompiled source code for DLL types (BCL, NuGet packages)."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "file": {
      "type": "string",
      "description": "File path to get symbol from (for location-based lookup)"
    },
    "line": {
      "type": "integer",
      "description": "Line number (1-based) for location-based lookup"
    },
    "column": {
      "type": "integer",
      "description": "Column number (1-based) for location-based lookup"
    },
    "symbolName": {
      "type": "string",
      "description": "Fully qualified symbol name for name-based lookup (e.g. 'System.String')"
    }
  }
}
```

**Example Input #1 - Workspace symbol:**

```json
{
  "file": "/Users/daniel/src/MyProject/Program.cs",
  "line": 15,
  "column": 13
}
```

**Example Output #1 - Source Location (workspace symbol):**

```json
{
  "success": true,
  "isSourceLocation": true,
  "filePath": "/Users/daniel/src/MyProject/Models/User.cs",
  "line": 8,
  "column": 18,
  "assembly": "MyProject, Version=1.0.0.0"
}
```

**Example Input #2 - DLL symbol:**

```json
{
  "symbolName": "System.String"
}
```

**Example Output #2 - Decompiled Source (DLL symbol):**

```json
{
  "success": true,
  "isSourceLocation": false,
  "decompiledSource": "// System.String\nusing System.Collections;\nusing System.Collections.Generic;\nusing System.Globalization;\nusing System.Runtime.CompilerServices;\nusing System.Runtime.InteropServices;\nusing System.Text;\n\nnamespace System\n{\n    [Serializable]\n    [TypeForwardedFrom(\"mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\")]\n    public sealed class String : IComparable, IEnumerable, IConvertible, IEnumerable<char>, IComparable<string>, IEquatable<string>, ICloneable\n    {\n        public static readonly string Empty;\n\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public extern String(char[] value);\n\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public extern String(char[] value, int startIndex, int length);\n\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public extern String(char c, int count);\n\n        [CLSCompliant(false)]\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public unsafe extern String(char* value);\n\n        [CLSCompliant(false)]\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public unsafe extern String(char* value, int startIndex, int length);\n\n        [CLSCompliant(false)]\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public unsafe extern String(sbyte* value);\n\n        [CLSCompliant(false)]\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public unsafe extern String(sbyte* value, int startIndex, int length);\n\n        [CLSCompliant(false)]\n        [MethodImpl(MethodImplOptions.InternalCall)]\n        public unsafe extern String(sbyte* value, int startIndex, int length, Encoding enc);\n\n        public object Clone() { }\n        public static int Compare(string strA, string strB) { }\n        public static int Compare(string strA, string strB, bool ignoreCase) { }\n        // ... hundreds more lines ...",
  "assembly": "System.Private.CoreLib, Version=10.0.0.0",
  "package": null
}
```

**Usability Analysis:**

✅ **Good:**
- Clear distinction between source locations and decompiled code via `isSourceLocation`
- For workspace symbols, returns concise location reference (LLM can use Read tool if needed)
- Decompilation enables understanding of BCL and NuGet dependencies

⚠️ **Potential Issues:**
- **MAJOR ISSUE**: Decompiled source can be MASSIVE (System.String is ~3000 lines)
- **MAJOR ISSUE**: Obfuscated DLLs will return unreadable garbage
- No way to request "signature only" instead of full decompilation
- No indication of decompilation quality or whether source was obfuscated
- For partial classes, unclear if all partial declarations are included

**Recommendations:**
- **CRITICAL**: Add `includeFullSource` parameter (default: false)
  - When false: return only type signature and member signatures (no method bodies)
  - When true: return full decompiled source
- Add `isObfuscated` detection heuristic and warning
- For workspace symbols, consider adding a code snippet around the definition (like VS Code peek)
- Add `decompiledLineCount` field to warn about large outputs
- Consider caching decompiled sources for performance

---

### 6. `get_type_members`

**Description:**
"Get the full definition of a type with all its members. Returns complete source code for workspace types or decompiled source for DLL types (BCL, NuGet packages)."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "typeName": {
      "type": "string",
      "description": "Fully qualified type name (e.g. 'System.String', 'SimpleProject.Calculator')"
    },
    "includeInherited": {
      "type": "boolean",
      "description": "Include inherited members (not yet implemented, reserved for future use)",
      "default": false
    }
  },
  "required": ["typeName"]
}
```

**Example Input #1 - Workspace type:**

```json
{
  "typeName": "MyProject.Calculator"
}
```

**Example Output #1 - Workspace Type:**

```json
{
  "success": true,
  "typeName": "Calculator",
  "namespace": "MyProject",
  "assembly": "MyProject, Version=1.0.0.0",
  "package": null,
  "isFromWorkspace": true,
  "filePath": "/Users/daniel/src/MyProject/Calculator.cs",
  "sourceCode": "using System;\n\nnamespace MyProject\n{\n    public class Calculator\n    {\n        public int Add(int a, int b) => a + b;\n        public int Subtract(int a, int b) => a - b;\n        public int Multiply(int a, int b) => a * b;\n        public double Divide(int a, int b)\n        {\n            if (b == 0)\n                throw new DivideByZeroException();\n            return (double)a / b;\n        }\n    }\n}"
}
```

**Example Input #2 - DLL type:**

```json
{
  "typeName": "System.Collections.Generic.Dictionary"
}
```

**Example Output #2 - Decompiled DLL Type:**

```json
{
  "success": true,
  "typeName": "Dictionary",
  "namespace": "System.Collections.Generic",
  "assembly": "System.Private.CoreLib, Version=10.0.0.0",
  "package": null,
  "isFromWorkspace": false,
  "filePath": null,
  "sourceCode": "// System.Collections.Generic.Dictionary<TKey, TValue>\nusing System;\nusing System.Collections;\nusing System.Collections.Generic;\nusing System.Diagnostics;\nusing System.Runtime.CompilerServices;\nusing System.Runtime.Serialization;\n\nnamespace System.Collections.Generic\n{\n    [Serializable]\n    [DebuggerTypeProxy(typeof(IDictionaryDebugView<,>))]\n    [DebuggerDisplay(\"Count = {Count}\")]\n    [TypeForwardedFrom(\"mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089\")]\n    public class Dictionary<TKey, TValue> : IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable, IDictionary, ICollection, IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>, ISerializable, IDeserializationCallback\n    {\n        private struct Entry\n        {\n            public uint hashCode;\n            public int next;\n            public TKey key;\n            public TValue value;\n        }\n\n        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IEnumerator, IDictionaryEnumerator\n        {\n            // ... implementation details ...\n        }\n\n        // ... hundreds more lines of implementation ...\n    }\n}"
}
```

**Usability Analysis:**

✅ **Good:**
- Returns complete, compilable source code
- `isFromWorkspace` clearly distinguishes workspace vs DLL types
- For workspace types, includes file path for reference
- Useful for understanding type structure

⚠️ **Potential Issues:**
- **SAME MAJOR ISSUES AS `get_definition`**:
  - Decompiled source can be enormous (Dictionary is ~2000+ lines)
  - Obfuscated DLLs will be unreadable
- `includeInherited` parameter doesn't work yet - confusing ✅ **FIXED** (removed in later version)
- No way to get just signatures without bodies ✅ **FIXED** (added `includeImplementation` parameter)
- For generic types, unclear how to specify type arguments in `typeName`

**Recommendations:**
- **CRITICAL**: Same as `get_definition` - add parameter to control detail level ✅ **FIXED**
- Remove `includeInherited` parameter until implemented (or implement it) ✅ **FIXED** (removed)
- Add examples in description showing how to query generic types
- Add `memberCount` field to warn about large types ✅ **FIXED** (added `lineCount` field)
- Consider alternative "list members only" mode that returns just signatures ✅ **FIXED** (implemented)

---

### 7. `get_code_actions`

**Description:**
"Get available code actions (fixes and refactorings) for a file. Returns all actions if line is omitted, or actions at a specific location if line is provided."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "file": {
      "type": "string",
      "description": "File path"
    },
    "line": {
      "type": "integer",
      "description": "Optional: specific line number (1-based). If omitted, returns all actions for the entire file."
    },
    "column": {
      "type": "integer",
      "description": "Optional: start column number (1-based) when line is specified"
    },
    "endLine": {
      "type": "integer",
      "description": "Optional: end line number (1-based) for range selection"
    },
    "endColumn": {
      "type": "integer",
      "description": "Optional: end column number (1-based) for range selection"
    },
    "diagnosticIds": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Optional: filter to specific diagnostic IDs, e.g. ['CS0103', 'IDE0005']"
    }
  },
  "required": ["file"]
}
```

**Example Input #1 - All actions in file:**

```json
{
  "file": "/Users/daniel/src/MyProject/Program.cs"
}
```

**Example Output #1:**

```json
{
  "success": true,
  "count": 23,
  "actions": [
    {
      "id": "action-1a2b3c4d",
      "title": "Remove unnecessary usings",
      "kind": "QuickFix",
      "diagnosticIds": ["IDE0005"]
    },
    {
      "id": "action-5e6f7g8h",
      "title": "Add null check",
      "kind": "Refactor",
      "diagnosticIds": []
    },
    {
      "id": "action-9i0j1k2l",
      "title": "Extract method",
      "kind": "Refactor",
      "diagnosticIds": []
    }
  ]
}
```

**Example Input #2 - Specific location with diagnostic filter:**

```json
{
  "file": "/Users/daniel/src/MyProject/Program.cs",
  "line": 15,
  "column": 13,
  "diagnosticIds": ["CS0103"]
}
```

**Example Output #2:**

```json
{
  "success": true,
  "count": 2,
  "actions": [
    {
      "id": "action-m3n4o5p6",
      "title": "Generate field 'undefinedVariable'",
      "kind": "QuickFix",
      "diagnosticIds": ["CS0103"]
    },
    {
      "id": "action-q7r8s9t0",
      "title": "Generate local 'undefinedVariable'",
      "kind": "QuickFix",
      "diagnosticIds": ["CS0103"]
    }
  ]
}
```

**Usability Analysis:**

✅ **Good:**
- Very flexible filtering (location, range, diagnostic IDs)
- `diagnosticIds` in response helps understand what each action fixes
- `kind` helps distinguish fixes vs refactorings
- Action IDs are opaque but usable with `apply_code_action`

⚠️ **Potential Issues:**
- Getting ALL actions for entire file could return hundreds of actions
- No pagination for large result sets
- Action ID format not documented (is it stable across requests?)
- No indication of action priority or "recommended" actions
- No preview of what the action will do

**Recommendations:**
- Add `maxResults` parameter
- Clarify in description that action IDs are only valid for the current session
- Consider adding `priority` or `isRecommended` field
- Consider adding brief description of what will change
- For whole-file queries, consider grouping by line number

---

### 8. `apply_code_action`

**Description:**
"Apply a code action (fix or refactoring) identified by its ID. Use preview mode to see changes before applying them."

**Parameters:**

```json
{
  "type": "object",
  "properties": {
    "actionId": {
      "type": "string",
      "description": "The ID of the code action to apply (from get_code_actions)"
    },
    "preview": {
      "type": "boolean",
      "description": "Optional: if true, return a preview of changes without applying them. Default: false",
      "default": false
    }
  },
  "required": ["actionId"]
}
```

**Example Input - Preview mode:**

```json
{
  "actionId": "action-1a2b3c4d",
  "preview": true
}
```

**Example Output - Preview:**

```json
{
  "success": true,
  "preview": true,
  "changesCount": 1,
  "changes": [
    {
      "filePath": "/Users/daniel/src/MyProject/Program.cs",
      "hasOriginalContent": true,
      "hasModifiedContent": true,
      "originalContent": "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\n\nnamespace MyProject\n{\n    class Program\n    {\n        static void Main(string[] args)\n        {\n            Console.WriteLine(\"Hello\");\n        }\n    }\n}",
      "modifiedContent": "using System;\n\nnamespace MyProject\n{\n    class Program\n    {\n        static void Main(string[] args)\n        {\n            Console.WriteLine(\"Hello\");\n        }\n    }\n}",
      "diff": "@@ -1,7 +1,5 @@\n using System;\n-using System.Collections.Generic;\n-using System.Linq;\n-using System.Text;\n \n namespace MyProject\n {\n"
    }
  ]
}
```

**Example Input - Apply mode:**

```json
{
  "actionId": "action-1a2b3c4d",
  "preview": false
}
```

**Example Output - Applied:**

```json
{
  "success": true,
  "preview": false,
  "changesCount": 1,
  "changes": [
    {
      "filePath": "/Users/daniel/src/MyProject/Program.cs",
      "hasOriginalContent": false,
      "hasModifiedContent": false,
      "originalContent": null,
      "modifiedContent": null,
      "diff": "@@ -1,7 +1,5 @@\n using System;\n-using System.Collections.Generic;\n-using System.Linq;\n-using System.Text;\n \n namespace MyProject\n {\n"
    }
  ]
}
```

**Usability Analysis:**

✅ **Good:**
- Preview mode is excellent for safety
- Provides both full content and unified diff
- Supports multi-file changes
- Clear success/failure indication

⚠️ **Potential Issues:**
- In non-preview mode, original/modified content is null but still has placeholder fields
- No way to know if action ID is still valid (workspace might have changed)
- For multi-file changes, could be verbose
- Diff format assumes client can parse unified diff format
- No rollback mechanism if something goes wrong

**Recommendations:**
- Simplify non-preview response to just show diff and file paths
- Add `isStale` warning if workspace has changed since action was discovered
- Consider alternative diff formats (e.g., JSON-based line changes)
- Add `canUndo` field to indicate if changes are reversible
- Consider adding optional `fileFilter` to only apply changes to specific files

---

## Summary of Key Usability Concerns

### Critical Issues

1. **Decompiled Source Size** (`get_definition`, `get_type_members`)
   - Can return thousands of lines for BCL types
   - **Impact:** Token limit exhaustion, slow responses, poor UX
   - **Fix:** Add parameter to control detail level (signatures only vs full source)

2. **Obfuscated DLL Handling** (`get_definition`, `get_type_members`)
   - Decompiled obfuscated code is unreadable
   - **Impact:** Confusing, useless output
   - **Fix:** Add obfuscation detection and warning

3. **Unlimited Result Sets** (`get_diagnostics`, `find_references`, `get_code_actions`)
   - No pagination or limits
   - **Impact:** Massive responses for large workspaces
   - **Fix:** Add `maxResults` and `offset` parameters with sensible defaults

### Moderate Issues

4. **Workspace vs DLL Distinction** (`get_definition`)
   - For workspace symbols, tool returns file location (client must use Read tool)
   - For DLL symbols, tool returns full source
   - **Impact:** Inconsistent - workspace case requires extra step
   - **Fix:** Consider adding source snippet for workspace symbols too

5. **Missing Fields** (`HasFix` in diagnostics, `DefinitionLocation` in symbol info)
   - Fields present but always empty
   - **Impact:** Misleading, suggests unimplemented features
   - **Fix:** Remove or implement properly

6. **Generic Type Handling** (`get_type_members`)
   - Unclear how to query `Dictionary<string, int>`
   - **Impact:** Confusion, potential errors
   - **Fix:** Add examples to description

### Minor Issues

7. **Long Signatures** (`get_symbol_info`)
   - Type signatures can span multiple lines
   - **Impact:** Verbose, hard to scan
   - **Fix:** Consider truncation with option to expand

8. **Action ID Lifetime** (`apply_code_action`)
   - Not documented when IDs become invalid
   - **Impact:** Potential errors with stale IDs
   - **Fix:** Document lifetime and add staleness detection

---

## Recommendations by Priority

### High Priority (Implement Soon)

1. Add `maxResults`/`offset` to all tools that return lists
2. Add detail-level parameter to `get_definition` and `get_type_members`
3. Remove or implement `HasFix` field in diagnostics
4. Add obfuscation detection for decompiled types

### Medium Priority

5. Add `IsFromWorkspace` field to `get_symbol_info`
6. Improve error messages with actionable guidance
7. Document generic type syntax for `get_type_members`
8. Add source snippets for workspace symbols in `get_definition`

### Low Priority (Nice to Have)

9. Add grouping/summaries to reduce verbosity
10. Add priority/recommendation hints to code actions
11. Expand context snippets in `find_references` to multi-line
12. Add rollback/undo capability for `apply_code_action`

---

## Overall Assessment

**Strengths:**
- Comprehensive coverage of C# language server features
- Flexible dual-mode lookups (location vs name)
- Good separation of concerns across tools
- Excellent decompilation capability

**Weaknesses:**
- Potential for massive responses (need limits/pagination)
- Decompiled source needs better controls
- Some inconsistencies in response formats
- Missing safeguards for edge cases

**Recommendation:**
The server is functionally strong but needs **usability polish** before production use. Focus on:
1. Response size controls
2. Better handling of decompiled content
3. Consistency improvements

With these changes, the server will be production-ready for integration with Claude Code and Cursor.

# Migration Notes: Xunit → NUnit Test Utilities

This document explains the adaptations made when copying test utilities from the MCP C# SDK (which uses Xunit) to our project (which uses NUnit 4).

## Source Files

Original location: `~/src/mcp/csharp-sdk/tests/Common/Utils/`

Files copied and adapted:
1. ✅ `LoggedTest.cs` - Adapted for NUnit
2. ✅ `MockLoggerProvider.cs` - No changes needed (framework-agnostic)
3. ❌ `DelegatingTestOutputHelper.cs` - Not needed (Xunit-specific)
4. ❌ `XunitLoggerProvider.cs` - Replaced with NUnitLoggerProvider

Files not copied:
- `MockHttpHandler.cs` - HTTP mocking, not needed yet
- `NodeHelpers.cs` - Node.js helpers, not needed
- `ProcessExtensions.cs` - Process utilities, not needed yet
- `TestServerTransport.cs` - MCP server transport testing, may need later

## Key Differences

### Xunit vs NUnit Test Output

**Xunit approach:**
```csharp
public LoggedTest(ITestOutputHelper testOutputHelper)
{
    _delegatingTestOutputHelper = new()
    {
        CurrentTestOutputHelper = testOutputHelper,
    };
    XunitLoggerProvider = new XunitLoggerProvider(_delegatingTestOutputHelper);
}
```

**NUnit approach:**
```csharp
public LoggedTest()
{
    // NUnit uses TestContext.Progress for test output
    // No parameter needed - TestContext is globally accessible
    NUnitLoggerProvider = new NUnitLoggerProvider();
}
```

### Test Output Mechanism

**Xunit:**
- Uses `ITestOutputHelper` interface passed to test constructor
- Requires `DelegatingTestOutputHelper` wrapper for field-based scenarios
- Test output: `testOutputHelper.WriteLine(message)`

**NUnit:**
- Uses static `TestContext.Progress` property
- No dependency injection needed
- Test output: `TestContext.Progress.WriteLine(message)`

### Usage Differences

**Xunit test:**
```csharp
public class MyTests : LoggedTest
{
    public MyTests(ITestOutputHelper output) : base(output)
    {
    }
}
```

**NUnit test:**
```csharp
[TestFixture]
internal class MyTests : LoggedTest
{
    // No constructor parameter needed
}
```

## What We Kept

### MockLoggerProvider (unchanged)

This class is framework-agnostic and works identically in both Xunit and NUnit:
- Captures log messages in a `ConcurrentQueue`
- Useful for assertions
- No dependency on test framework

### LoggedTest (adapted)

Core functionality preserved:
- Provides `LoggerFactory` for tests
- Provides `MockLoggerProvider` for assertions
- Implements `IDisposable` for cleanup

Changes:
- Removed `ITestOutputHelper` parameter
- Removed `DelegatingTestOutputHelper` (Xunit-specific)
- Renamed `XunitLoggerProvider` → `NUnitLoggerProvider`

### NUnitLoggerProvider (new)

Functionally equivalent to `XunitLoggerProvider`:
- Same log formatting
- Same timestamp format
- Same exception handling
- Different output mechanism (`TestContext.Progress` vs `ITestOutputHelper`)

## Testing Verification

All existing integration tests pass with the new utilities:
```bash
dotnet test tests/CSharperMcp.Server.IntegrationTests
# Result: Passed! - 41 tests, 0 failures
```

## Future Considerations

If we need additional utilities from the MCP SDK test suite:
- `TestServerTransport.cs` - For testing MCP server protocol directly
- `MockHttpHandler.cs` - For HTTP client testing
- `ProcessExtensions.cs` - For process management in tests

These can be adapted using the same pattern:
1. Copy the file
2. Update namespace to `CSharperMcp.Server.IntegrationTests.TestUtils`
3. Change access modifier to `internal`
4. Replace Xunit-specific code with NUnit equivalents
5. Verify compilation and test execution

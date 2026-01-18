# Test Utilities

This directory contains test utility classes adapted from the MCP C# SDK test suite.

## Overview

These utilities provide logging infrastructure for integration tests, allowing tests to:
1. Write logs to NUnit test output (visible during test execution)
2. Capture logs for assertion in tests
3. Set up a proper `ILoggerFactory` for testing components that depend on logging

## Usage

### Basic Usage with LoggedTest Base Class

```csharp
[TestFixture]
internal class MyIntegrationTests : LoggedTest
{
    private MyService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        // Use the inherited LoggerFactory to create loggers for your components
        var logger = LoggerFactory.CreateLogger<MyService>();
        _sut = new MyService(logger);
    }

    [Test]
    public void MyTest()
    {
        // Act
        _sut.DoSomething();

        // Assert - you can check logged messages using MockLoggerProvider
        MockLoggerProvider.LogMessages.Should().Contain(
            log => log.Message.Contains("Expected log message")
        );
    }

    [TearDown]
    public void TearDown()
    {
        (_sut as IDisposable)?.Dispose();
    }
}
```

### Manual Usage Without Base Class

If you don't want to inherit from `LoggedTest`, you can set up logging manually:

```csharp
[TestFixture]
internal class MyOtherTests
{
    private ILoggerFactory _loggerFactory = null!;
    private MockLoggerProvider _mockLoggerProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLoggerProvider = new MockLoggerProvider();
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new NUnitLoggerProvider());
            builder.AddProvider(_mockLoggerProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory?.Dispose();
    }
}
```

## Classes

### LoggedTest

Base class for tests that need logging infrastructure. Automatically sets up:
- `NUnitLoggerProvider` - writes logs to NUnit test output
- `MockLoggerProvider` - captures logs for assertions
- `LoggerFactory` - pre-configured logger factory

Implements `IDisposable` to clean up the logger factory.

### NUnitLoggerProvider

Logger provider that writes to NUnit's `TestContext.Progress`. This makes logs visible in test output during execution.

### MockLoggerProvider

Logger provider that captures all log messages in a `ConcurrentQueue`. Useful for asserting that specific log messages were written during test execution.

The `LogMessages` property contains tuples of:
- `string Category` - Logger category name
- `LogLevel LogLevel` - Log level
- `EventId EventId` - Event ID
- `string Message` - Formatted log message
- `Exception? Exception` - Exception (if any)

## Notes

- All classes use `internal` access modifier following project conventions
- The test projects have `InternalsVisibleTo` configured in `Directory.Build.props`
- Logging infrastructure is automatically disposed when using `LoggedTest` base class

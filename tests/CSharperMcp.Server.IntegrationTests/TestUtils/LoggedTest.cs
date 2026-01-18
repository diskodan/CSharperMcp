using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests.TestUtils;

/// <summary>
/// Base class for tests that need logging infrastructure.
/// Provides a LoggerFactory configured to write to NUnit test output and capture logs for assertions.
/// </summary>
internal class LoggedTest : IDisposable
{
    public LoggedTest()
    {
        NUnitLoggerProvider = new NUnitLoggerProvider();
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(NUnitLoggerProvider);
            builder.AddProvider(MockLoggerProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    public ILoggerFactory LoggerFactory { get; set; }
    public ILoggerProvider NUnitLoggerProvider { get; }
    public MockLoggerProvider MockLoggerProvider { get; } = new();

    public virtual void Dispose()
    {
        LoggerFactory?.Dispose();
    }
}

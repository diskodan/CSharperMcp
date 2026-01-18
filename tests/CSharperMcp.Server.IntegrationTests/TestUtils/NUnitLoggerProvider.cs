using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace CSharperMcp.Server.IntegrationTests.TestUtils;

internal class NUnitLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new NUnitLogger(categoryName);
    }

    public void Dispose()
    {
    }

    private class NUnitLogger(string category) : ILogger
    {
        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var sb = new StringBuilder();

            var timestamp = DateTimeOffset.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var prefix = $"| [{timestamp}] {category} {logLevel}: ";
            var lines = formatter(state, exception);
            sb.Append(prefix);
            sb.Append(lines);

            if (exception is not null)
            {
                sb.AppendLine();
                sb.Append(exception.ToString());
            }

            // Write to NUnit test output
            TestContext.Progress.WriteLine(sb.ToString());
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

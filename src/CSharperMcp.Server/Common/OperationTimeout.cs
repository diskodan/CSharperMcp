namespace CSharperMcp.Server.Common;

/// <summary>
/// Constants and utilities for operation timeouts.
/// </summary>
internal static class OperationTimeout
{
    /// <summary>
    /// Default timeout for most operations (2 minutes).
    /// Prevents hangs from complex code analysis or large solutions.
    /// </summary>
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Shorter timeout for quick operations like diagnostics retrieval (30 seconds).
    /// </summary>
    public static readonly TimeSpan Quick = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Longer timeout for expensive operations like workspace initialization (5 minutes).
    /// </summary>
    public static readonly TimeSpan Long = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Creates a CancellationTokenSource with the specified timeout.
    /// </summary>
    public static CancellationTokenSource CreateWithTimeout(TimeSpan timeout)
        => new CancellationTokenSource(timeout);

    /// <summary>
    /// Links an external cancellation token with a timeout.
    /// </summary>
    public static CancellationTokenSource CreateLinked(CancellationToken externalToken, TimeSpan timeout)
    {
        var timeoutCts = new CancellationTokenSource(timeout);
        return CancellationTokenSource.CreateLinkedTokenSource(externalToken, timeoutCts.Token);
    }
}

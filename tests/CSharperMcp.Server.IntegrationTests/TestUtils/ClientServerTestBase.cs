using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.IO.Pipelines;

namespace CSharperMcp.Server.IntegrationTests.TestUtils;

/// <summary>
/// Base class for integration tests that need to test MCP tools in-process.
/// Creates a connected client-server pair using in-memory pipes.
/// Derived classes can override ConfigureServices to register custom services and tools.
/// </summary>
internal abstract class ClientServerTestBase : LoggedTest, IAsyncDisposable
{
    private readonly Pipe _clientToServerPipe = new();
    private readonly Pipe _serverToClientPipe = new();
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;
    private static bool _msbuildRegistered;
    private static readonly object _msbuildLock = new();

    protected ClientServerTestBase()
    {
        // CRITICAL: Ensure MSBuild is registered before any Roslyn types are loaded
        // Use lock to ensure thread safety in parallel test execution
        lock (_msbuildLock)
        {
            if (!_msbuildRegistered)
            {
                MSBuildLocator.RegisterDefaults();
                _msbuildRegistered = true;
            }
        }

        ServiceCollection sc = new();
        sc.AddLogging();
        sc.AddSingleton(NUnitLoggerProvider);
        sc.AddSingleton<ILoggerProvider>(MockLoggerProvider);

        // Register core services that tools depend on
        sc.AddSingleton<WorkspaceManager>();
        sc.AddSingleton<RoslynService>();
        sc.AddSingleton<DecompilerService>();
        sc.AddSingleton<CodeActionsService>();

        // Add WorkspaceConfiguration without initial workspace (test can initialize explicitly)
        sc.AddSingleton(new WorkspaceConfiguration { InitialWorkspacePath = null });

        // Configure MCP server with in-memory transport
        var mcpBuilder = sc
            .AddMcpServer()
            .WithStreamServerTransport(
                _clientToServerPipe.Reader.AsStream(),
                _serverToClientPipe.Writer.AsStream());

        // Allow derived classes to customize service registration
        ConfigureServices(sc, mcpBuilder);

        ServiceProvider = sc.BuildServiceProvider(validateScopes: true);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CurrentContext.CancellationToken);
        Server = ServiceProvider.GetRequiredService<global::ModelContextProtocol.Server.McpServer>();
        _serverTask = Server.RunAsync(_cts.Token);
    }

    protected global::ModelContextProtocol.Server.McpServer Server { get; }

    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Override this method to register custom services and configure the MCP server.
    /// This is called during construction before the server starts.
    /// </summary>
    /// <param name="services">Service collection to add services to</param>
    /// <param name="mcpServerBuilder">MCP server builder for configuration</param>
    protected virtual void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        // Default implementation registers all tools from the server assembly
        mcpServerBuilder.WithToolsFromAssembly();
    }

    /// <summary>
    /// Creates an MCP client connected to the test server via in-memory pipes.
    /// Use this to test tools by calling them through the MCP protocol.
    /// </summary>
    protected async Task<McpClient> CreateMcpClientForServer(McpClientOptions? clientOptions = null)
    {
        return await McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: _clientToServerPipe.Writer.AsStream(),
                _serverToClientPipe.Reader.AsStream(),
                LoggerFactory),
            clientOptions: clientOptions,
            loggerFactory: LoggerFactory,
            cancellationToken: TestContext.CurrentContext.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _clientToServerPipe.Writer.Complete();
        _serverToClientPipe.Writer.Complete();

        // Wait for server task to complete, but don't throw if it's already faulted
        try
        {
            await _serverTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is triggered
        }

        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _cts.Dispose();
        Dispose();
        GC.SuppressFinalize(this);
    }
}

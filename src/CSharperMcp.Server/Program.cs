using CSharperMcp.Server.Extensions;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

// CRITICAL: Register MSBuild before any Roslyn types are loaded
MSBuildLocator.RegisterDefaults();

var builder = Host.CreateApplicationBuilder(args);

// Configure workspace options from command-line arguments
builder.Services.Configure<WorkspaceConfiguration>(options =>
{
    // Parse --workspace parameter
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--workspace" && i + 1 < args.Length)
        {
            options.InitialWorkspacePath = args[i + 1];
            break;
        }
    }
});

// Configure code action filtering (use defaults)
builder.Services.Configure<CodeActionFilterConfiguration>(_ => { });

// Configure logging to stderr (stdout is reserved for MCP JSON-RPC)
builder.Services.AddLogging(logging =>
{
    logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register services that tools depend on
builder.Services.AddSingleton<WorkspaceManager>();
builder.Services.AddSingleton<RoslynService>();
builder.Services.AddSingleton<DecompilerService>();
builder.Services.AddSingleton<CodeActionProviderService>();
builder.Services.AddSingleton<CodeActionsService>();

// Register MCP server with tools auto-discovered from this assembly
var mcpServer = builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

// Filter out initialize_workspace tool if workspace was auto-initialized
mcpServer.AddListToolsFilter(next =>
    async (request, cancellationToken) =>
    {
        var workspaceConfig = request.Services?.GetService<IOptions<WorkspaceConfiguration>>();

        // Only filter if workspace was auto-initialized
        if (workspaceConfig?.Value.InitialWorkspacePath.IsNullOrEmpty() ?? true)
        {
            return await next(request, cancellationToken);
        }

        var logger = request.Services?.GetService<ILogger<Program>>();
        logger?.LogInformation(
            "Workspace auto-initialized from --workspace parameter, hiding initialize_workspace tool"
        );

        // Call the next handler in the pipeline to get the full list of tools
        var result = await next(request, cancellationToken);

        // Remove initialize_workspace from the tools list since workspace is already initialized
        result.Tools = result.Tools.Where(t => t.Name != "initialize_workspace").ToList();

        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug("Filtered tools list, {Count} tools remaining", result.Tools.Count);
        }

        return result;
    }
);

var app = builder.Build();

// Auto-initialize workspace if --workspace parameter was provided
var workspaceConfig = app.Services.GetRequiredService<IOptions<WorkspaceConfiguration>>().Value;
if (!workspaceConfig.InitialWorkspacePath.IsNullOrEmpty())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var workspaceManager = app.Services.GetRequiredService<WorkspaceManager>();

    logger.LogInformation(
        "Auto-initializing workspace from --workspace parameter: {Path}",
        workspaceConfig.InitialWorkspacePath
    );
    var (success, message, projectCount) = await workspaceManager.InitializeAsync(workspaceConfig.InitialWorkspacePath);

    if (success)
    {
        logger.LogInformation(
            "Workspace initialized successfully: {ProjectCount} project(s) loaded",
            projectCount
        );
    }
    else
    {
        logger.LogError("Failed to initialize workspace: {Message}", message);
    }
}

await app.RunAsync();

using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using CSharperMcp.Server.Workspace;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Tools;

// CRITICAL: Register MSBuild before any Roslyn types are loaded
MSBuildLocator.RegisterDefaults();

// Parse command-line arguments
string? workspacePath = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--workspace" && i + 1 < args.Length)
    {
        workspacePath = args[i + 1];
        break;
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register services that tools depend on
builder.Services.AddSingleton<WorkspaceManager>();
builder.Services.AddSingleton<RoslynService>();
builder.Services.AddSingleton<DecompilerService>();
builder.Services.AddSingleton<CodeActionsService>();

// Store workspace path for conditional tool registration
builder.Services.AddSingleton(new WorkspaceConfiguration { InitialWorkspacePath = workspacePath });

// Register MCP server with tools auto-discovered from this assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Auto-initialize workspace if --workspace parameter was provided
if (!string.IsNullOrEmpty(workspacePath))
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var workspaceManager = app.Services.GetRequiredService<WorkspaceManager>();

    logger.LogInformation("Auto-initializing workspace from --workspace parameter: {Path}", workspacePath);
    var (success, message, projectCount) = await workspaceManager.InitializeAsync(workspacePath);

    if (success)
    {
        logger.LogInformation("Workspace initialized successfully: {ProjectCount} project(s) loaded", projectCount);
    }
    else
    {
        logger.LogError("Failed to initialize workspace: {Message}", message);
    }
}

await app.RunAsync();

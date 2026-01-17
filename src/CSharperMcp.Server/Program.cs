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

// Register MCP server with tools auto-discovered from this assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

await app.RunAsync();

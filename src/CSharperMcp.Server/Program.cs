using CSharperMcp.Server.Extensions;
using CSharperMcp.Server.Models;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

// CRITICAL: Register MSBuild before any Roslyn types are loaded
MSBuildLocator.RegisterDefaults();

var builder = Host.CreateApplicationBuilder(args);

// Build configuration from multiple YAML sources (hierarchical merging)
// 1. User's config directory (global preferences, optional)
var userConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config",
    "csharp-er-mcp.yml"
);
builder.Configuration.AddYamlFile(userConfigPath, optional: true, reloadOnChange: false);

// 2. Project config directory (only if --workspace or --workspace-from-cwd provided, optional)
// Parse --workspace parameter or --workspace-from-cwd flag to determine project config location
string? workspacePath = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--workspace" && i + 1 < args.Length)
    {
        workspacePath = args[i + 1];
        break;
    }
}

if (
    (workspacePath == null && args.Any(x => x == "--workspace-from-cwd"))
    || workspacePath == "$(pwd)" //shrug
    || workspacePath == "${workspaceFolder}"
)
{
    workspacePath = Environment.CurrentDirectory;
}

if (workspacePath != null)
{
    // Use directory if .sln/.csproj, otherwise use path as-is
    var projectConfigDir = File.Exists(workspacePath)
        ? Path.GetDirectoryName(workspacePath)
        : workspacePath;

    if (projectConfigDir != null)
    {
        var projectConfigPath = Path.Combine(projectConfigDir, ".config", "csharp-er-mcp.yml");
        builder.Configuration.AddYamlFile(projectConfigPath, optional: true, reloadOnChange: false);
    }

    // Auto-disable initialize_workspace tool when workspace is provided via --workspace or --workspace-from-cwd
    builder.Configuration.AddInMemoryCollection(
        new Dictionary<string, string?> { { "tools:initialize_workspace:isEnabled", "false" } }
    );

    // Configure workspace options from command-line arguments
    builder.Services.Configure<WorkspaceConfiguration>(options =>
    {
        options.InitialWorkspacePath = workspacePath;
    });
}

// Configure MCP server options with default instructions (later configuration overrides earlier)
// 1. Set default server instructions
builder.Services.Configure<McpServerOptions>(options =>
{
    var initWorkspacePart =
        workspacePath == null
            ? @"
IMPORTANT: All tools require an initialized workspace. Call initialize_workspace first."
            : "";
    options.ServerInstructions =
        $@"This MCP server provides semantic C# language server capabilities for LLMs.
{initWorkspacePart}

Features:
- Workspace initialization from .sln or .csproj files
- Compiler diagnostics (errors, warnings, analyzer messages)
- Symbol resolution and navigation (including NuGet packages and DLLs)
- Find references across workspace
- Code actions and refactorings
- Decompilation of types from referenced assemblies

Use this server to gain IDE-like understanding of C# codebases without grepping files.";
});

// 2. Allow YAML configuration to override server instructions
builder.Services.Configure<McpServerOptions>(builder.Configuration.GetSection("mcp"));

// Configure code action filtering (use defaults)
builder.Services.Configure<CodeActionFilterConfiguration>(_ => { });

// Bind tool descriptions configuration from hierarchical YAML sources
builder.Services.Configure<ToolDescriptionsConfiguration>(options =>
{
    builder.Configuration.GetSection("tools").Bind(options.Tools);
});

// Configure logging to stderr (stdout is reserved for MCP JSON-RPC)
builder.Services.AddLogging(logging =>
{
    logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    logging.SetMinimumLevel(LogLevel.Information);
    builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
});

// Register services that tools depend on
builder.Services.AddSingleton<WorkspaceManager>();
builder.Services.AddSingleton<RoslynService>();
builder.Services.AddSingleton<DecompilerService>();
builder.Services.AddSingleton<CodeActionProviderService>();
builder.Services.AddSingleton<CodeActionsService>();

// Register MCP server with tools auto-discovered from this assembly
var mcpServer = builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

// Add filter for custom tool descriptions and disabled tools from configuration
mcpServer.AddListToolsFilter(next =>
    async (request, cancellationToken) =>
    {
        var result = await next(request, cancellationToken);

        var toolConfig = request.Services!.GetRequiredService<
            IOptions<ToolDescriptionsConfiguration>
        >();
        if (!toolConfig.Value.Tools.Any())
            return result;

        var logger = request.Services!.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "Applying custom tool descriptions for {Count} tools",
            toolConfig.Value.Tools.Count
        );

        // Apply description overrides
        foreach (var tool in result.Tools)
        {
            if (!toolConfig.Value.Tools.TryGetValue(tool.Name, out var toolOverride))
                continue;

            if (toolOverride.Description != null)
            {
                tool.Description = toolOverride.Description;
                logger.LogDebug("Overrode description for tool: {Name}", tool.Name);
            }
        }

        // Filter out disabled tools
        result.Tools = result
            .Tools.Where(t =>
            {
                if (!toolConfig.Value.Tools.TryGetValue(t.Name, out var toolOverride))
                    return true; // Keep tools not in config

                if (!toolOverride.IsEnabled)
                {
                    logger.LogDebug("Filtering out disabled tool: {Name}", t.Name);
                    return false;
                }

                return true;
            })
            .ToList();

        return result;
    }
);

var app = builder.Build();

// Auto-initialize workspace if --workspace or --workspace-from-cwd was provided
var workspaceConfig = app.Services.GetRequiredService<IOptions<WorkspaceConfiguration>>().Value;
if (!workspaceConfig.InitialWorkspacePath.IsNullOrEmpty())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var workspaceManager = app.Services.GetRequiredService<WorkspaceManager>();

    logger.LogInformation(
        "Auto-initializing workspace from command-line parameter: {Path}",
        workspaceConfig.InitialWorkspacePath
    );
    var (success, message, projectCount) = await workspaceManager.InitializeAsync(
        workspaceConfig.InitialWorkspacePath
    );

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

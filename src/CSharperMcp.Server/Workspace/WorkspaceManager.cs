using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using CSharperMcp.Server.Common;
using CSharperMcp.Server.Extensions;

namespace CSharperMcp.Server.Workspace;

internal class WorkspaceManager(ILogger<WorkspaceManager> logger) : IDisposable
{
    private readonly List<string> _workspaceDiagnostics = [];
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;

    public Solution? CurrentSolution => _solution;
    public MSBuildWorkspace? Workspace => _workspace;
    public bool IsInitialized => _solution != null;
    public IReadOnlyList<string> WorkspaceDiagnostics => _workspaceDiagnostics;
    public string? WorkspaceRoot { get; private set; }

    public async Task<(bool Success, string Message, int ProjectCount)> InitializeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return (false, "Path cannot be empty", 0);
        }

        using var timeoutCts = OperationTimeout.CreateLinked(cancellationToken, OperationTimeout.Long);

        try
        {
            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailed);

            var solutionPath = await DiscoverSolutionAsync(path);
            if (solutionPath == null)
            {
                return (false, $"No .sln, .slnx, or .csproj found at {path}", 0);
            }

            logger.LogInformation("Loading solution from {Path}", solutionPath);
            _solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: timeoutCts.Token);
            WorkspaceRoot = Path.GetDirectoryName(solutionPath);

            var projectCount = _solution.Projects.Count();
            logger.LogInformation("Successfully loaded solution with {Count} projects", projectCount);

            return (true, $"Loaded solution with {projectCount} projects", projectCount);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            logger.LogError("Workspace initialization timed out after {Timeout}", OperationTimeout.Long);
            return (false, $"Operation timed out after {OperationTimeout.Long.TotalMinutes} minutes", 0);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Workspace initialization was cancelled");
            return (false, "Operation was cancelled", 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize workspace");
            return (false, ex.Message, 0);
        }
    }

    private async Task<string?> DiscoverSolutionAsync(string path)
    {
        // If path is a .sln or .slnx file, use it directly
        if (File.Exists(path) && (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                                   path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            return path;
        }

        // If path is a .csproj file, use it directly
        if (File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        // If path is a directory, search for .sln and .slnx files
        if (Directory.Exists(path))
        {
            var slnFiles = Directory.GetFiles(path, "*.sln", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(path, "*.slnx", SearchOption.TopDirectoryOnly))
                .ToArray();

            if (slnFiles.Length > 0)
            {
                // Prefer solution file with same name as directory
                var dirName = Path.GetFileName(path);
                var matchingSln = slnFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(dirName, StringComparison.OrdinalIgnoreCase));

                return matchingSln ?? slnFiles[0];
            }

            // Fall back to .csproj files
            var csprojFiles = Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojFiles.Length > 0)
            {
                return csprojFiles[0];
            }
        }

        return null;
    }

    private void OnWorkspaceFailed(WorkspaceDiagnosticEventArgs e)
    {
        var message = $"[{e.Diagnostic.Kind}] {e.Diagnostic.Message}";
        _workspaceDiagnostics.Add(message);
        logger.LogWarning("Workspace diagnostic: {Message}", message);
    }

    public void UpdateCurrentSolution(Solution solution)
    {
        _solution = solution;
        logger.LogDebug("Updated current solution");
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }
}

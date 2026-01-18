using System.Diagnostics;
using System.Text.Json.Nodes;

namespace CSharperMcp.Server.IntegrationTests.McpServer;

[TestFixture]
public class WorkspaceParameterFilterTests
{
    [Test]
    public async Task ToolsList_WithWorkspaceParameter_HidesInitializeWorkspaceTool()
    {
        // Arrange
        var fixturePath = GetFixturePath("SimpleSolution");
        var serverPath = GetServerPath();

        using var serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{serverPath}\" --no-build -- --workspace \"{fixturePath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        serverProcess.Start();
        var stdin = serverProcess.StandardInput;
        var stdout = serverProcess.StandardOutput;

        try
        {
            // Initialize
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}}");
            await stdin.FlushAsync();
            var initResponse = await ReadJsonResponseAsync(stdout, 1);
            initResponse.Should().NotBeNull();

            // Send initialized notification
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            await stdin.FlushAsync();

            // Act - Request tools list
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
            await stdin.FlushAsync();
            var listResponse = await ReadJsonResponseAsync(stdout, 2);

            // Assert
            listResponse.Should().NotBeNull();
            var tools = listResponse!["result"]?["tools"]?.AsArray();
            tools.Should().NotBeNull();

            var toolNames = tools!.Select(t => t!["name"]!.GetValue<string>()).ToList();

            // initialize_workspace should be hidden when --workspace parameter is used
            toolNames.Should().NotContain("initialize_workspace");

            // Other tools should still be present
            toolNames.Should().Contain("get_diagnostics");
        }
        finally
        {
            if (!serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(1000);
            }
        }
    }

    [Test]
    public async Task ToolsList_WithoutWorkspaceParameter_ShowsInitializeWorkspaceTool()
    {
        // Arrange
        var serverPath = GetServerPath();

        using var serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{serverPath}\" --no-build",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        serverProcess.Start();
        var stdin = serverProcess.StandardInput;
        var stdout = serverProcess.StandardOutput;

        try
        {
            // Initialize
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}}");
            await stdin.FlushAsync();
            var initResponse = await ReadJsonResponseAsync(stdout, 1);
            initResponse.Should().NotBeNull();

            // Send initialized notification
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            await stdin.FlushAsync();

            // Act - Request tools list
            await stdin.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
            await stdin.FlushAsync();
            var listResponse = await ReadJsonResponseAsync(stdout, 2);

            // Assert
            listResponse.Should().NotBeNull();
            var tools = listResponse!["result"]?["tools"]?.AsArray();
            tools.Should().NotBeNull();

            var toolNames = tools!.Select(t => t!["name"]!.GetValue<string>()).ToList();

            // initialize_workspace SHOULD be visible when --workspace parameter is NOT used
            toolNames.Should().Contain("initialize_workspace");
            toolNames.Should().Contain("get_diagnostics");
        }
        finally
        {
            if (!serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(1000);
            }
        }
    }

    private static async Task<JsonNode?> ReadJsonResponseAsync(StreamReader stdout, int expectedId)
    {
        var maxAttempts = 100;
        for (int i = 0; i < maxAttempts; i++)
        {
            var line = await stdout.ReadLineAsync();
            if (line == null)
                throw new Exception("Server closed connection");

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("{"))
                continue;

            try
            {
                var json = JsonNode.Parse(line);
                if (json?["id"]?.GetValue<int>() == expectedId)
                    return json;
            }
            catch
            {
                continue;
            }
        }

        throw new Exception($"Failed to receive response for request {expectedId}");
    }

    private static string GetFixturePath(string fixtureName)
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        return Path.Combine(testDir, "Fixtures", fixtureName);
    }

    private static string GetServerPath()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        return Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "CSharperMcp.Server", "CSharperMcp.Server.csproj");
    }
}

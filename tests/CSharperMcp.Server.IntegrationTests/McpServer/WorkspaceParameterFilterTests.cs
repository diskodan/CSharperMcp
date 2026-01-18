using ModelContextProtocol.Client;

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

        var transportOptions = new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", serverPath, "--no-build", "--", "--workspace", fixturePath],
            Name = "CSharperMcp.Server"
        };

        // Act
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(transportOptions));
        var tools = await client.ListToolsAsync();

        // Assert
        tools.Should().NotBeNull();

        var toolNames = tools.Select(t => t.Name).ToList();

        // initialize_workspace should be hidden when --workspace parameter is used
        toolNames.Should().NotContain("initialize_workspace");

        // Other tools should still be present
        toolNames.Should().Contain("get_diagnostics");
    }

    [Test]
    public async Task ToolsList_WithoutWorkspaceParameter_ShowsInitializeWorkspaceTool()
    {
        // Arrange
        var serverPath = GetServerPath();

        var transportOptions = new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", serverPath, "--no-build"],
            Name = "CSharperMcp.Server"
        };

        // Act
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(transportOptions));
        var tools = await client.ListToolsAsync();

        // Assert
        tools.Should().NotBeNull();

        var toolNames = tools.Select(t => t.Name).ToList();

        // initialize_workspace SHOULD be visible when --workspace parameter is NOT used
        toolNames.Should().Contain("initialize_workspace");
        toolNames.Should().Contain("get_diagnostics");
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

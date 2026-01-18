using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CSharperMcp.Server.IntegrationTests.McpServer;

internal class McpServerIntegrationTests
{
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

    [Test]
    public async Task ToolsList_ReturnsExpectedTools()
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
        toolNames.Should().Contain("initialize_workspace");
        toolNames.Should().Contain("get_diagnostics");
    }

    [Test]
    public async Task InitializeWorkspace_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        var serverPath = GetServerPath();
        var fixturePath = GetFixturePath("SimpleSolution");
        var transportOptions = new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", serverPath, "--no-build"],
            Name = "CSharperMcp.Server"
        };

        await using var client = await McpClient.CreateAsync(new StdioClientTransport(transportOptions));

        // Act
        var result = await client.CallToolAsync("initialize_workspace", new Dictionary<string, object?>
        {
            ["path"] = fixturePath
        });

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNull().And.HaveCountGreaterThan(0);

        var textContent = result.Content[0] as TextContentBlock;
        textContent.Should().NotBeNull();

        var json = JsonDocument.Parse(textContent!.Text);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("projectCount").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task InitializeWorkspace_WithInvalidPath_ReturnsFailure()
    {
        // Arrange
        var serverPath = GetServerPath();
        var transportOptions = new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", serverPath, "--no-build"],
            Name = "CSharperMcp.Server"
        };

        await using var client = await McpClient.CreateAsync(new StdioClientTransport(transportOptions));

        // Act
        var result = await client.CallToolAsync("initialize_workspace", new Dictionary<string, object?>
        {
            ["path"] = "/nonexistent/path/to/solution"
        });

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNull().And.HaveCountGreaterThan(0);

        var textContent = result.Content[0] as TextContentBlock;
        textContent.Should().NotBeNull();

        var json = JsonDocument.Parse(textContent!.Text);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task GetDiagnostics_AfterInitialize_ReturnsDiagnostics()
    {
        // Arrange
        var serverPath = GetServerPath();
        var fixturePath = GetFixturePath("SolutionWithErrors");
        var transportOptions = new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", serverPath, "--no-build"],
            Name = "CSharperMcp.Server"
        };

        await using var client = await McpClient.CreateAsync(new StdioClientTransport(transportOptions));

        // Initialize workspace first
        await client.CallToolAsync("initialize_workspace", new Dictionary<string, object?>
        {
            ["path"] = fixturePath
        });

        // Act
        var result = await client.CallToolAsync("get_diagnostics", new Dictionary<string, object?>
        {
            ["severity"] = "error"
        });

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNull().And.HaveCountGreaterThan(0);

        var textContent = result.Content[0] as TextContentBlock;
        textContent.Should().NotBeNull();
        textContent!.Text.Should().Contain("CS0103"); // Undeclared variable error
    }

    [Test]
    public async Task GetDiagnostics_BeforeInitialize_ReturnsError()
    {
        // Arrange
        var serverPath = GetServerPath();
        var transportOptions = new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", serverPath, "--no-build"],
            Name = "CSharperMcp.Server"
        };

        await using var client = await McpClient.CreateAsync(new StdioClientTransport(transportOptions));

        // Act - call get_diagnostics WITHOUT initializing workspace first
        var result = await client.CallToolAsync("get_diagnostics", new Dictionary<string, object?>
        {
            ["severity"] = "error"
        });

        // Assert
        result.Should().NotBeNull();

        // Either isError flag is set, or content contains error message
        if (result.IsError == true)
        {
            result.Content.Should().NotBeNull();
        }
        else
        {
            result.Content.Should().NotBeNull().And.HaveCountGreaterThan(0);
            var textContent = result.Content[0] as TextContentBlock;
            textContent.Should().NotBeNull();

            var text = textContent!.Text;
            // The response should indicate an error condition
            (text.Contains("not initialized") || text.Contains("error") || text.Contains("Error"))
                .Should().BeTrue($"expected error message but got: {text}");
        }
    }
}

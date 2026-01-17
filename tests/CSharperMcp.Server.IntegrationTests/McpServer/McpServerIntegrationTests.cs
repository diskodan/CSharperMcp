using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSharperMcp.Server.IntegrationTests.McpServer;

[TestFixture]
public class McpServerIntegrationTests
{
    private Process? _serverProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _requestId;

    [SetUp]
    public void SetUp()
    {
        _requestId = 0;

        var serverPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
            "src", "CSharperMcp.Server", "CSharperMcp.Server.csproj");
        serverPath = Path.GetFullPath(serverPath);

        _serverProcess = new Process
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

        _serverProcess.Start();
        _stdin = _serverProcess.StandardInput;
        _stdout = _serverProcess.StandardOutput;
    }

    [TearDown]
    public void TearDown()
    {
        _stdin?.Dispose();
        _stdout?.Dispose();

        if (_serverProcess is { HasExited: false })
        {
            _serverProcess.Kill();
            _serverProcess.WaitForExit(1000);
        }
        _serverProcess?.Dispose();
    }

    private async Task<JsonNode?> SendRequestAsync(string method, JsonNode? parameters = null)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = ++_requestId,
            ["method"] = method
        };

        if (parameters != null)
            request["params"] = parameters;

        var requestJson = request.ToJsonString();
        await _stdin!.WriteLineAsync(requestJson);
        await _stdin.FlushAsync();

        // Read lines until we get our response
        var expectedId = _requestId;
        while (true)
        {
            var line = await _stdout!.ReadLineAsync();
            if (line == null)
                throw new Exception("Server closed connection");

            if (!line.StartsWith("{"))
                continue; // Skip non-JSON lines (logging)

            var response = JsonNode.Parse(line);
            if (response?["id"]?.GetValue<int>() == expectedId)
                return response;
        }
    }

    private async Task SendNotificationAsync(string method, JsonNode? parameters = null)
    {
        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        };

        if (parameters != null)
            notification["params"] = parameters;

        await _stdin!.WriteLineAsync(notification.ToJsonString());
        await _stdin.FlushAsync();
    }

    private async Task InitializeMcpAsync()
    {
        var initParams = new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "test",
                ["version"] = "1.0"
            }
        };

        var response = await SendRequestAsync("initialize", initParams);
        response.Should().NotBeNull();
        response!["result"].Should().NotBeNull();

        await SendNotificationAsync("notifications/initialized");
    }

    private static string GetFixturePath(string fixtureName)
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        return Path.Combine(testDir, "Fixtures", fixtureName);
    }

    [Test]
    public async Task ToolsList_ReturnsExpectedTools()
    {
        // Arrange
        await InitializeMcpAsync();

        // Act
        var response = await SendRequestAsync("tools/list");

        // Assert
        response.Should().NotBeNull();
        var tools = response!["result"]?["tools"]?.AsArray();
        tools.Should().NotBeNull();

        var toolNames = tools!.Select(t => t!["name"]!.GetValue<string>()).ToList();
        toolNames.Should().Contain("initialize_workspace");
        toolNames.Should().Contain("get_diagnostics");
    }

    [Test]
    public async Task InitializeWorkspace_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        await InitializeMcpAsync();
        var fixturePath = GetFixturePath("SimpleSolution");

        // Act
        var response = await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = "initialize_workspace",
            ["arguments"] = new JsonObject
            {
                ["path"] = fixturePath
            }
        });

        // Assert
        response.Should().NotBeNull();
        var result = response!["result"];
        result.Should().NotBeNull();

        // The result should contain content with success info
        var content = result!["content"]?.AsArray();
        content.Should().NotBeNull();
        content!.Count.Should().BeGreaterThan(0);

        var text = content[0]!["text"]?.GetValue<string>();
        text.Should().NotBeNull();
        text.Should().Contain("\"success\":true");
        text.Should().Contain("\"projectCount\":1");
    }

    [Test]
    public async Task InitializeWorkspace_WithInvalidPath_ReturnsFailure()
    {
        // Arrange
        await InitializeMcpAsync();

        // Act
        var response = await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = "initialize_workspace",
            ["arguments"] = new JsonObject
            {
                ["path"] = "/nonexistent/path/to/solution"
            }
        });

        // Assert
        response.Should().NotBeNull();
        var result = response!["result"];
        result.Should().NotBeNull();

        var content = result!["content"]?.AsArray();
        content.Should().NotBeNull();

        var text = content![0]!["text"]?.GetValue<string>();
        text.Should().Contain("\"success\":false");
    }

    [Test]
    public async Task GetDiagnostics_AfterInitialize_ReturnsDiagnostics()
    {
        // Arrange
        await InitializeMcpAsync();
        var fixturePath = GetFixturePath("SolutionWithErrors");

        // Initialize workspace first
        await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = "initialize_workspace",
            ["arguments"] = new JsonObject { ["path"] = fixturePath }
        });

        // Act
        var response = await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = "get_diagnostics",
            ["arguments"] = new JsonObject { ["severity"] = "error" }
        });

        // Assert
        response.Should().NotBeNull();
        var result = response!["result"];
        result.Should().NotBeNull();

        var content = result!["content"]?.AsArray();
        content.Should().NotBeNull();

        var text = content![0]!["text"]?.GetValue<string>();
        text.Should().NotBeNull();
        text.Should().Contain("CS0103"); // Undeclared variable error
    }

    [Test]
    public async Task GetDiagnostics_BeforeInitialize_ReturnsError()
    {
        // Arrange
        await InitializeMcpAsync();
        // Don't initialize workspace

        // Act
        var response = await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = "get_diagnostics",
            ["arguments"] = new JsonObject { ["severity"] = "error" }
        });

        // Assert
        response.Should().NotBeNull();

        // Check for either JSON-RPC error or error message in content
        var error = response!["error"];
        if (error != null)
        {
            // JSON-RPC error format
            error["message"]?.GetValue<string>().Should().Contain("not initialized");
        }
        else
        {
            // Tool returned error as content or isError flag
            var result = response["result"];
            result.Should().NotBeNull();

            var isError = result!["isError"]?.GetValue<bool>() ?? false;
            var content = result["content"]?.AsArray();

            // Either isError is true, or the content contains an error message
            if (!isError && content != null && content.Count > 0)
            {
                var text = content[0]!["text"]?.GetValue<string>() ?? "";
                // The response should indicate an error condition
                (text.Contains("not initialized") || text.Contains("error") || text.Contains("Error"))
                    .Should().BeTrue($"expected error message but got: {text}");
            }
            else
            {
                isError.Should().BeTrue("workspace not initialized should cause an error");
            }
        }
    }
}

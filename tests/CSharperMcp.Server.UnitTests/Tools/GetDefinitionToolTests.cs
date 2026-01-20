using System.Text.Json;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Tools;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.UnitTests.Tools;

/// <summary>
/// Tests for parameter validation in GetDefinitionTool.
/// These tests verify that the tool correctly validates mutually exclusive parameters.
/// </summary>
internal class GetDefinitionToolTests
{
    [Test]
    public async Task GetDefinition_ShouldReturnError_WhenBothLocationAndSymbolNameProvided()
    {
        // Arrange
        const string file = "/path/to/file.cs";
        const int line = 10;
        const int column = 5;
        const string symbolName = "System.String";

        // Act
        var result = await GetDefinitionTool.GetDefinition(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            file,
            line,
            column,
            symbolName);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("Provide either (file + line + column) OR symbolName, not both");
    }

    [Test]
    public async Task GetDefinition_ShouldReturnError_WhenNeitherLocationNorSymbolNameProvided()
    {
        // Act
        var result = await GetDefinitionTool.GetDefinition(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>());

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("Must provide either (file + line + column) OR symbolName");
    }

    [Test]
    public async Task GetDefinition_ShouldReturnError_WhenOnlyFileProvided()
    {
        // Arrange
        const string file = "/path/to/file.cs";

        // Act
        var result = await GetDefinitionTool.GetDefinition(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            file: file);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("When using location-based lookup, you must provide all three parameters");
    }

    [Test]
    public async Task GetDefinition_ShouldReturnError_WhenFileAndSymbolNameProvided()
    {
        // Arrange
        const string file = "/path/to/file.cs";
        const string symbolName = "System.String";

        // Act
        var result = await GetDefinitionTool.GetDefinition(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            file: file,
            symbolName: symbolName);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("Provide either (file + line + column) OR symbolName, not both");
    }

    [Test]
    public async Task GetDefinition_ShouldReturnError_WhenLineAndSymbolNameProvided()
    {
        // Arrange
        const int line = 10;
        const string symbolName = "System.String";

        // Act
        var result = await GetDefinitionTool.GetDefinition(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            line: line,
            symbolName: symbolName);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("Provide either (file + line + column) OR symbolName, not both");
    }

    [Test]
    public async Task GetDefinition_ShouldReturnError_WhenColumnAndSymbolNameProvided()
    {
        // Arrange
        const int column = 5;
        const string symbolName = "System.String";

        // Act
        var result = await GetDefinitionTool.GetDefinition(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            column: column,
            symbolName: symbolName);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("Provide either (file + line + column) OR symbolName, not both");
    }
}

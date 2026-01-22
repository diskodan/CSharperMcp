using System.Text.Json;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Tools;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.UnitTests.Tools;

/// <summary>
/// Tests for parameter validation in SymbolInfoTool.
/// These tests verify that the tool correctly validates required parameters.
/// </summary>
internal class SymbolInfoToolTests
{
    [Test]
    public async Task GetSymbolInfo_ShouldReturnError_WhenFileIsEmpty()
    {
        // Arrange
        const string file = "";
        const int line = 10;
        const int column = 5;

        // Act
        var result = await SymbolInfoTool.GetSymbolInfo(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            file,
            line,
            column);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("file parameter is required");
    }

    [Test]
    public async Task GetSymbolInfo_ShouldReturnError_WhenFileIsWhitespace()
    {
        // Arrange
        const string file = "   ";
        const int line = 10;
        const int column = 5;

        // Act
        var result = await SymbolInfoTool.GetSymbolInfo(
            null!,  // Service won't be called due to validation error
            Mock.Of<ILogger<RoslynService>>(),
            file,
            line,
            column);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should()
            .Contain("file parameter is required");
    }
}

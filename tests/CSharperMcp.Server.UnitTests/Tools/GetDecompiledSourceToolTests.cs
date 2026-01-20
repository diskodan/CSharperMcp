using System.Text.Json;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Tools;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.UnitTests.Tools;

/// <summary>
/// Tests for parameter validation in GetDecompiledSourceTool.
/// These tests verify that the tool correctly validates required parameters.
/// Service interaction tests are covered by integration tests.
/// </summary>
internal class GetDecompiledSourceToolTests
{
    [Test]
    public async Task GetDecompiledSource_ShouldReturnError_WhenTypeNameIsNull()
    {
        // Act
        var result = await GetDecompiledSourceTool.GetDecompiledSource(
            null!,  // Service won't be called due to validation error
            null!,
            Mock.Of<ILogger<RoslynService>>(),
            typeName: null!);

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should().Contain("typeName parameter is required");
    }

    [Test]
    public async Task GetDecompiledSource_ShouldReturnError_WhenTypeNameIsEmpty()
    {
        // Act
        var result = await GetDecompiledSourceTool.GetDecompiledSource(
            null!,  // Service won't be called due to validation error
            null!,
            Mock.Of<ILogger<RoslynService>>(),
            typeName: "");

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should().Contain("typeName parameter is required");
    }

    [Test]
    public async Task GetDecompiledSource_ShouldReturnError_WhenTypeNameIsWhitespace()
    {
        // Act
        var result = await GetDecompiledSourceTool.GetDecompiledSource(
            null!,  // Service won't be called due to validation error
            null!,
            Mock.Of<ILogger<RoslynService>>(),
            typeName: "   ");

        // Assert
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("message").GetString().Should().Contain("typeName parameter is required");
    }
}

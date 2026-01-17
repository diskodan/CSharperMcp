using CSharperMcp.Server.Workspace;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.UnitTests.Workspace;

[TestFixture]
public class WorkspaceManagerTests
{
    private Mock<ILogger<WorkspaceManager>> _loggerMock = null!;
    private WorkspaceManager _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<WorkspaceManager>>();
        _sut = new WorkspaceManager(_loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _sut?.Dispose();
    }

    [Test]
    public void IsInitialized_ShouldReturnFalse_WhenNotInitialized()
    {
        // Act
        var result = _sut.IsInitialized;

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task InitializeAsync_ShouldReturnFalse_WhenPathDoesNotExist()
    {
        // Arrange
        var invalidPath = "/nonexistent/path";

        // Act
        var (success, message, projectCount) = await _sut.InitializeAsync(invalidPath);

        // Assert
        success.Should().BeFalse();
        projectCount.Should().Be(0);
    }
}

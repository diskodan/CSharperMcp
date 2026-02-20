using CSharperMcp.Server.Workspace;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.UnitTests.Workspace;

internal class WorkspaceManagerTests
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

    [Test]
    public async Task InitializeAsync_ShouldDiscoverSlnxFile_WhenPassedDirectly()
    {
        // Arrange: create a temp .slnx file so DiscoverSolutionAsync can find it
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var slnxPath = Path.Combine(tempDir, "Test.slnx");
        await File.WriteAllTextAsync(slnxPath, "<Solution />");

        try
        {
            // Act
            var (success, message, projectCount) = await _sut.InitializeAsync(slnxPath);

            // Assert: should NOT get "no .sln or .csproj found" — it found the file and attempted to load it
            message.Should().NotContain("No .sln or .csproj found");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task InitializeAsync_ShouldDiscoverSlnxFile_WhenPassedDirectory()
    {
        // Arrange: create a temp dir with a .slnx file
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "Test.slnx"), "<Solution />");

        try
        {
            // Act
            var (success, message, projectCount) = await _sut.InitializeAsync(tempDir);

            // Assert: should NOT get "no .sln or .csproj found" — it found the .slnx and attempted to load it
            message.Should().NotContain("No .sln or .csproj found");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

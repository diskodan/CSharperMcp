using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests.Workspace;

internal class WorkspaceManagerIntegrationTests
{
    private Mock<ILogger<WorkspaceManager>> _loggerMock = null!;
    private WorkspaceManager _sut = null!;

    [OneTimeSetUp]
    public static void OneTimeSetUp()
    {
        // MSBuildLocator must be called once before any Roslyn types are used
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

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

    private static string GetFixturePath(string fixtureName)
    {
        // Fixtures are copied to the test output directory
        var testDir = TestContext.CurrentContext.TestDirectory;
        return Path.Combine(testDir, "Fixtures", fixtureName);
    }

    [Test]
    public async Task InitializeAsync_WithSimpleSolution_LoadsSuccessfully()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");

        // Act
        var (success, message, projectCount) = await _sut.InitializeAsync(solutionPath);

        // Assert
        success.Should().BeTrue(because: $"initialization should succeed: {message}");
        projectCount.Should().Be(1, because: "SimpleSolution has exactly one project");
        _sut.IsInitialized.Should().BeTrue();
        _sut.CurrentSolution.Should().NotBeNull();
        _sut.WorkspaceDiagnostics.Should().BeEmpty();
    }

    [Test]
    public async Task InitializeAsync_WithDirectory_FindsAndLoadsSolution()
    {
        // Arrange - pass directory path instead of .sln path
        var directoryPath = GetFixturePath("SimpleSolution");

        // Act
        var (success, message, projectCount) = await _sut.InitializeAsync(directoryPath);

        // Assert
        success.Should().BeTrue(because: $"should find .sln in directory: {message}");
        projectCount.Should().Be(1);
    }

    [Test]
    public async Task InitializeAsync_WithNonExistentPath_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(GetFixturePath("NonExistent"), "DoesNotExist.sln");

        // Act
        var (success, message, projectCount) = await _sut.InitializeAsync(nonExistentPath);

        // Assert
        success.Should().BeFalse();
        projectCount.Should().Be(0);
        _sut.IsInitialized.Should().BeFalse();
    }

    [Test]
    public async Task InitializeAsync_WithSolutionWithErrors_StillLoads()
    {
        // Arrange - SolutionWithErrors has compile errors but should still load
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");

        // Act
        var (success, message, projectCount) = await _sut.InitializeAsync(solutionPath);

        // Assert - workspace should load even with compile errors
        success.Should().BeTrue(because: $"workspace loading should succeed even with errors: {message}");
        projectCount.Should().Be(1);
        _sut.CurrentSolution.Should().NotBeNull();
    }

    [Test]
    public async Task InitializeAsync_WithSolutionWithNuGet_LoadsSuccessfully()
    {
        // Arrange - SolutionWithNuGet references Newtonsoft.Json NuGet package
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");

        // Act
        var (success, message, projectCount) = await _sut.InitializeAsync(solutionPath);

        // Assert
        success.Should().BeTrue(because: $"initialization should succeed: {message}");
        projectCount.Should().Be(1, because: "SolutionWithNuGet has exactly one project");
        _sut.IsInitialized.Should().BeTrue();
        _sut.CurrentSolution.Should().NotBeNull();

        // Verify the project loaded its NuGet reference
        var project = _sut.CurrentSolution!.Projects.First();
        project.MetadataReferences.Should().Contain(r =>
            r.Display != null && r.Display.Contains("Newtonsoft.Json"),
            because: "project should have Newtonsoft.Json assembly reference");
    }
}

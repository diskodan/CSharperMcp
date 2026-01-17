using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests.Services;

[TestFixture]
public class RoslynServiceIntegrationTests
{
    private Mock<ILogger<WorkspaceManager>> _workspaceLoggerMock = null!;
    private Mock<ILogger<RoslynService>> _roslynLoggerMock = null!;
    private WorkspaceManager _workspaceManager = null!;
    private RoslynService _sut = null!;

    [OneTimeSetUp]
    public static void OneTimeSetUp()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    [SetUp]
    public void SetUp()
    {
        _workspaceLoggerMock = new Mock<ILogger<WorkspaceManager>>();
        _roslynLoggerMock = new Mock<ILogger<RoslynService>>();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _sut = new RoslynService(_workspaceManager, _roslynLoggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _workspaceManager?.Dispose();
    }

    private static string GetFixturePath(string fixtureName)
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        return Path.Combine(testDir, "Fixtures", fixtureName);
    }

    [Test]
    public async Task GetDiagnosticsAsync_WithSolutionWithErrors_ReturnsExpectedErrors()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var diagnostics = (await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Warning)).ToList();

        // Assert - we expect specific errors from our test fixture:
        // CS0103 (line 8): undeclared variable
        // CS0029 (line 14): type conversion error
        // CS1061 (line 21): missing method
        // CS0414 (line 6): unused field warning
        diagnostics.Should().NotBeEmpty();

        var errorCodes = diagnostics.Select(d => d.Id).Distinct().ToList();
        errorCodes.Should().Contain("CS0103", because: "fixture has undeclared variable");
        errorCodes.Should().Contain("CS0029", because: "fixture has type conversion error");
        errorCodes.Should().Contain("CS1061", because: "fixture has missing method");
        errorCodes.Should().Contain("CS0414", because: "fixture has unused field warning");
    }

    [Test]
    public async Task GetDiagnosticsAsync_WithSimpleSolution_ReturnsNoDiagnostics()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - get errors only (not warnings)
        var diagnostics = (await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Error)).ToList();

        // Assert - SimpleSolution should compile cleanly
        diagnostics.Should().BeEmpty(because: "SimpleSolution has no errors");
    }

    [Test]
    public async Task GetDiagnosticsAsync_FilterByFile_ReturnsOnlyMatchingDiagnostics()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - filter to only ClassWithErrors.cs
        var diagnostics = (await _sut.GetDiagnosticsAsync(
            filePath: "ClassWithErrors.cs",
            minimumSeverity: DiagnosticSeverity.Warning)).ToList();

        // Assert - should only get diagnostics from ClassWithErrors.cs
        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().AllSatisfy(d =>
            d.Location.SourceTree?.FilePath.Should().EndWith("ClassWithErrors.cs"));
    }

    [Test]
    public async Task GetDiagnosticsAsync_FilterByLineRange_ReturnsOnlyMatchingDiagnostics()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - filter to lines 1-10 (should include CS0103 on line 8)
        var diagnostics = (await _sut.GetDiagnosticsAsync(
            startLine: 1,
            endLine: 10,
            minimumSeverity: DiagnosticSeverity.Error)).ToList();

        // Assert - should find CS0103 error around line 8
        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Contain(d => d.Id == "CS0103");
    }

    [Test]
    public async Task GetDiagnosticsAsync_FilterBySeverity_ReturnsOnlyMatchingSeverity()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - get errors only (should exclude CS0414 warning)
        var errorDiagnostics = (await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Error)).ToList();

        // Assert
        errorDiagnostics.Should().NotBeEmpty();
        errorDiagnostics.Should().AllSatisfy(d =>
            d.Severity.Should().Be(DiagnosticSeverity.Error));
        errorDiagnostics.Should().NotContain(d => d.Id == "CS0414",
            because: "CS0414 is a warning, not an error");
    }

    [Test]
    public async Task GetDiagnosticsAsync_WhenWorkspaceNotInitialized_ThrowsException()
    {
        // Arrange - don't initialize workspace

        // Act & Assert
        var act = () => _sut.GetDiagnosticsAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }
}

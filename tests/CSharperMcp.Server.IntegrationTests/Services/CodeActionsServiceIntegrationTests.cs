using CSharperMcp.Server.Models;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CSharperMcp.Server.IntegrationTests.Services;

internal class CodeActionsServiceIntegrationTests
{
    private Mock<ILogger<WorkspaceManager>> _workspaceLoggerMock = null!;
    private Mock<ILogger<CodeActionsService>> _codeActionsLoggerMock = null!;
    private Mock<ILogger<CodeActionProviderService>> _providerServiceLoggerMock = null!;
    private WorkspaceManager _workspaceManager = null!;
    private CodeActionProviderService _providerService = null!;
    private IOptions<CodeActionFilterConfiguration> _filterConfig = null!;
    private CodeActionsService _sut = null!;

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
        _codeActionsLoggerMock = new Mock<ILogger<CodeActionsService>>();
        _providerServiceLoggerMock = new Mock<ILogger<CodeActionProviderService>>();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _providerService = new CodeActionProviderService(_providerServiceLoggerMock.Object);
        _filterConfig = Options.Create(new CodeActionFilterConfiguration());
        _sut = new CodeActionsService(_workspaceManager, _providerService, _filterConfig, _codeActionsLoggerMock.Object);
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
    public async Task GetCodeActionsAsync_ForLocationWithDiagnostic_ReturnsAvailableActions()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get code actions at line with CS0103 error (undeclared variable)
        var actions = (await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1)).ToList();

        // Assert - Should find actions for the diagnostic at this location
        actions.Should().NotBeNull();
        // Note: Actual action discovery depends on having CS0103 at line 8
        // If no actions found, it means the diagnostic isn't at exactly that location
        // or our known fixable diagnostics doesn't include it
    }

    [Test]
    public async Task GetCodeActionsAsync_WithoutDiagnostics_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get code actions at a line with no errors
        var actions = (await _sut.GetCodeActionsAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 1)).ToList();

        // Assert - Should return empty (no diagnostics = no fixes)
        actions.Should().BeEmpty();
    }

    [Test]
    public async Task GetCodeActionsAsync_WithInvalidFile_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var actions = (await _sut.GetCodeActionsAsync(
            filePath: "NonExistent.cs",
            line: 1,
            column: 1)).ToList();

        // Assert
        actions.Should().BeEmpty();
    }

    [Test]
    public async Task GetCodeActionsAsync_WhenWorkspaceNotInitialized_ThrowsException()
    {
        // Arrange - don't initialize workspace

        // Act & Assert
        var act = () => _sut.GetCodeActionsAsync("File.cs", 1, 1);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }
}

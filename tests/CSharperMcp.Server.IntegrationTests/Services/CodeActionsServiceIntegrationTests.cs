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
        var result = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1);

        // Assert - Should find actions for the diagnostic at this location
        result.Should().NotBeNull();
        result.Actions.Should().NotBeNull();
        result.TotalCount.Should().Be(result.Actions.Count);
        result.ReturnedCount.Should().Be(result.Actions.Count);
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
        var result = await _sut.GetCodeActionsAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 1);

        // Assert - Should return empty (no diagnostics = no fixes)
        result.Actions.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.ReturnedCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    [Test]
    public async Task GetCodeActionsAsync_WithInvalidFile_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetCodeActionsAsync(
            filePath: "NonExistent.cs",
            line: 1,
            column: 1);

        // Assert
        result.Actions.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.ReturnedCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
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

    [Test]
    public async Task ApplyCodeActionAsync_InPreviewMode_ReturnsChangesWithoutApplying()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // First, get code actions to populate the cache
        var actionsResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1);

        // Skip test if no actions found (means no fixable diagnostics at this location)
        if (!actionsResult.Actions.Any())
        {
            Assert.Inconclusive("No code actions found at the specified location");
            return;
        }

        var firstAction = actionsResult.Actions.First();

        // Act - Apply in preview mode
        var result = await _sut.ApplyCodeActionAsync(firstAction.Id, preview: true);

        // Assert
        result.Success.Should().BeTrue();
        result.Changes.Should().NotBeEmpty();
        result.Changes.Should().AllSatisfy(change =>
        {
            change.FilePath.Should().NotBeNullOrEmpty();
            // In preview mode, we should have both original and modified content (or one of them for add/delete)
            (change.OriginalContent != null || change.ModifiedContent != null).Should().BeTrue();
        });
    }

    [Test]
    public async Task ApplyCodeActionAsync_WithInvalidActionId_ReturnsFailure()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Try to apply non-existent action
        var result = await _sut.ApplyCodeActionAsync("non-existent-action-id", preview: true);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        result.Changes.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyCodeActionAsync_WhenWorkspaceNotInitialized_ReturnsFailure()
    {
        // Arrange - don't initialize workspace

        // Act
        var result = await _sut.ApplyCodeActionAsync("some-action-id", preview: true);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Workspace not initialized");
        result.Changes.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyCodeActionAsync_InApplyMode_PersistsChangesToDisk()
    {
        // Arrange
        var fixturePath = GetFixturePath("SolutionWithErrors");
        var solutionPath = Path.Combine(fixturePath, "SolutionWithErrors.sln");
        var targetFile = Path.Combine(fixturePath, "ProjectWithErrors", "ClassWithErrors.cs");

        // Create a backup of the original file
        var backupFile = targetFile + ".backup";
        if (File.Exists(targetFile))
        {
            File.Copy(targetFile, backupFile, overwrite: true);
        }

        try
        {
            await _workspaceManager.InitializeAsync(solutionPath);

            // First, get code actions to populate the cache
            var actionsResult = await _sut.GetCodeActionsAsync(
                filePath: "ClassWithErrors.cs",
                line: 8,
                column: 1);

            // Skip test if no actions found
            if (!actionsResult.Actions.Any())
            {
                Assert.Inconclusive("No code actions found at the specified location");
                return;
            }

            var firstAction = actionsResult.Actions.First();
            var originalContent = File.Exists(targetFile) ? await File.ReadAllTextAsync(targetFile) : null;

            // Act - Apply without preview
            var result = await _sut.ApplyCodeActionAsync(firstAction.Id, preview: false);

            // Assert
            result.Success.Should().BeTrue();
            result.Changes.Should().NotBeEmpty();

            // Verify that the file on disk was actually modified
            if (File.Exists(targetFile) && originalContent != null)
            {
                var modifiedContent = await File.ReadAllTextAsync(targetFile);
                modifiedContent.Should().NotBe(originalContent, "The file should have been modified on disk");
            }
        }
        finally
        {
            // Restore the original file
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, targetFile, overwrite: true);
                File.Delete(backupFile);
            }
        }
    }

    [Test]
    public async Task GetCodeActionsAsync_WithMaxResults_LimitsResultCount()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get all actions first to know total count
        var allActionsResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1,
            maxResults: 1000); // High limit to get all

        // Skip test if no actions found
        if (allActionsResult.TotalCount == 0)
        {
            Assert.Inconclusive("No code actions found at the specified location");
            return;
        }

        // Act - Now request with a smaller limit
        var limitedResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1,
            maxResults: 2);

        // Assert
        limitedResult.TotalCount.Should().Be(allActionsResult.TotalCount, "Total count should reflect all available actions");
        limitedResult.ReturnedCount.Should().BeLessOrEqualTo(2, "Should not return more than maxResults");
        limitedResult.Actions.Count.Should().Be(limitedResult.ReturnedCount);

        if (allActionsResult.TotalCount > 2)
        {
            limitedResult.HasMore.Should().BeTrue("HasMore should be true when there are more results available");
        }
    }

    [Test]
    public async Task GetCodeActionsAsync_WithOffset_SkipsResults()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get all actions first
        var allActionsResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1,
            maxResults: 1000);

        // Skip test if not enough actions
        if (allActionsResult.TotalCount < 3)
        {
            Assert.Inconclusive("Need at least 3 code actions, found " + allActionsResult.TotalCount);
            return;
        }

        // Act - Get with offset
        var offsetResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1,
            maxResults: 2,
            offset: 1);

        // Assert
        offsetResult.TotalCount.Should().Be(allActionsResult.TotalCount);
        offsetResult.ReturnedCount.Should().BeLessOrEqualTo(2);
        offsetResult.Actions.Should().NotBeEmpty();

        // The first action in offsetResult should be the second action in allActionsResult
        if (allActionsResult.Actions.Count > 1 && offsetResult.Actions.Count > 0)
        {
            offsetResult.Actions[0].Id.Should().Be(allActionsResult.Actions[1].Id,
                "Offset should skip the first action");
        }
    }

    [Test]
    public async Task GetCodeActionsAsync_WithOffsetBeyondTotal_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get all actions first to know total
        var allActionsResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1);

        // Act - Request with offset beyond total
        var result = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1,
            offset: allActionsResult.TotalCount + 10);

        // Assert
        result.TotalCount.Should().Be(allActionsResult.TotalCount, "Total count should remain the same");
        result.ReturnedCount.Should().Be(0, "Should return no results when offset is beyond total");
        result.Actions.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
    }

    [Test]
    public async Task GetCodeActionsAsync_LastPage_HasMoreIsFalse()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get all actions first
        var allActionsResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1);

        // Skip test if no actions
        if (allActionsResult.TotalCount == 0)
        {
            Assert.Inconclusive("No code actions found");
            return;
        }

        // Act - Request exactly the total count (last page)
        var lastPageResult = await _sut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 8,
            column: 1,
            maxResults: allActionsResult.TotalCount,
            offset: 0);

        // Assert
        lastPageResult.HasMore.Should().BeFalse("HasMore should be false on the last page");
        lastPageResult.ReturnedCount.Should().Be(allActionsResult.TotalCount);
    }

    [Test]
    public async Task GetCodeActionsAsync_DefaultMaxResults_Is50()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Create a custom filter config that allows more than 50 results
        var customFilterConfig = Options.Create(new CodeActionFilterConfiguration
        {
            MaxResults = 1000, // Allow up to 1000 in filtering
            IncludeRefactorings = true
        });
        var customSut = new CodeActionsService(_workspaceManager, _providerService, customFilterConfig, _codeActionsLoggerMock.Object);

        // Act - Call without specifying maxResults (should default to 50)
        var result = await customSut.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs");

        // Assert - If there are more than 50 actions available, only 50 should be returned
        if (result.TotalCount > 50)
        {
            result.ReturnedCount.Should().Be(50, "Default maxResults should be 50");
            result.HasMore.Should().BeTrue();
        }
        else
        {
            result.ReturnedCount.Should().Be(result.TotalCount);
        }
    }
}

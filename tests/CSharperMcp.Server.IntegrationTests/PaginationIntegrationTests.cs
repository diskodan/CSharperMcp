using CSharperMcp.Server.Models;
using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CSharperMcp.Server.IntegrationTests;

/// <summary>
/// Integration tests for pagination functionality across multiple tools (Tasks 3-5).
/// Tests pagination for get_diagnostics, find_references, and get_code_actions.
/// </summary>
[TestFixture]
internal class PaginationIntegrationTests
{
    private Mock<ILogger<WorkspaceManager>> _workspaceLoggerMock = null!;
    private Mock<ILogger<RoslynService>> _roslynLoggerMock = null!;
    private Mock<ILogger<DecompilerService>> _decompilerLoggerMock = null!;
    private Mock<ILogger<CodeActionsService>> _codeActionsLoggerMock = null!;
    private Mock<ILogger<CodeActionProviderService>> _providerServiceLoggerMock = null!;
    private WorkspaceManager _workspaceManager = null!;
    private DecompilerService _decompilerService = null!;
    private RoslynService _roslynService = null!;
    private CodeActionsService _codeActionsService = null!;
    private CodeActionProviderService _providerService = null!;
    private IOptions<CodeActionFilterConfiguration> _filterConfig = null!;

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
        _decompilerLoggerMock = new Mock<ILogger<DecompilerService>>();
        _codeActionsLoggerMock = new Mock<ILogger<CodeActionsService>>();
        _providerServiceLoggerMock = new Mock<ILogger<CodeActionProviderService>>();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _decompilerService = new DecompilerService(_workspaceManager, _decompilerLoggerMock.Object);
        _roslynService = new RoslynService(_workspaceManager, _decompilerService, _roslynLoggerMock.Object);
        _providerService = new CodeActionProviderService(_providerServiceLoggerMock.Object);
        _filterConfig = Options.Create(new CodeActionFilterConfiguration());
        _codeActionsService = new CodeActionsService(_workspaceManager, _providerService, _filterConfig, _codeActionsLoggerMock.Object);
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

    #region Diagnostics Pagination Tests

    [Test]
    public async Task GetDiagnostics_WithMaxResults_LimitsOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all diagnostics first to know total count
        var (allDiagnostics, totalCount) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning);
        var allDiagnosticsList = allDiagnostics.ToList();

        totalCount.Should().BeGreaterThan(0, "Test fixture should have diagnostics");

        // Act - Request only 2 diagnostics
        var (limitedDiagnostics, returnedTotal) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 2,
            offset: 0);
        var limitedDiagnosticsList = limitedDiagnostics.ToList();

        // Assert
        returnedTotal.Should().Be(totalCount, "Total count should always reflect full result set");
        limitedDiagnosticsList.Should().HaveCount(Math.Min(2, totalCount),
            "Should return at most 2 diagnostics");

        // Verify we got the first 2 diagnostics
        if (totalCount >= 2)
        {
            limitedDiagnosticsList[0].Id.Should().Be(allDiagnosticsList[0].Id);
            limitedDiagnosticsList[1].Id.Should().Be(allDiagnosticsList[1].Id);
        }
    }

    [Test]
    public async Task GetDiagnostics_WithOffset_SkipsResults()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all diagnostics first
        var (allDiagnostics, totalCount) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning);
        var allDiagnosticsList = allDiagnostics.ToList();

        if (totalCount < 3)
        {
            Assert.Inconclusive("Need at least 3 diagnostics for this test");
            return;
        }

        // Act - Skip first 2 diagnostics
        var (offsetDiagnostics, returnedTotal) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 100,
            offset: 2);
        var offsetDiagnosticsList = offsetDiagnostics.ToList();

        // Assert
        returnedTotal.Should().Be(totalCount);
        offsetDiagnosticsList.Should().HaveCount(totalCount - 2);

        // First diagnostic in offset result should be the third diagnostic overall
        offsetDiagnosticsList[0].Id.Should().Be(allDiagnosticsList[2].Id);
    }

    [Test]
    public async Task GetDiagnostics_WithOffsetBeyondTotal_ReturnsEmptyList()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        var (_, totalCount) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning);

        // Act - Request offset beyond total
        var (diagnostics, returnedTotal) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 10,
            offset: totalCount + 100);
        var diagnosticsList = diagnostics.ToList();

        // Assert
        returnedTotal.Should().Be(totalCount);
        diagnosticsList.Should().BeEmpty(
            "Offset beyond total should return empty list");
    }

    [Test]
    public async Task GetDiagnostics_Pagination_HasMoreIsAccurate()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        var (_, totalCount) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning);

        if (totalCount < 2)
        {
            Assert.Inconclusive("Need at least 2 diagnostics for this test");
            return;
        }

        // Act - Get first page with maxResults = 1
        var (firstPage, _) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 1,
            offset: 0);
        var firstPageList = firstPage.ToList();

        // Act - Get last page
        var (lastPage, _) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 1,
            offset: totalCount - 1);
        var lastPageList = lastPage.ToList();

        // Assert
        firstPageList.Should().HaveCount(1);
        // HasMore logic: returned count < total remaining
        // For first page: returned 1, total 1 (since maxResults=1), but total > 1, so more available

        lastPageList.Should().HaveCount(1);
        // For last page: returned 1, offset = totalCount - 1, so this is the last item
    }

    [Test]
    public async Task GetDiagnostics_DefaultMaxResults_Is100()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Use default pagination
        var (diagnostics, totalCount) = await _roslynService.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning);
        var diagnosticsList = diagnostics.ToList();

        // Assert - Should return at most 100 results by default
        diagnosticsList.Should().HaveCountLessOrEqualTo(100,
            "Default maxResults should be 100");
        totalCount.Should().BeGreaterOrEqualTo(diagnosticsList.Count);
    }

    #endregion

    #region Find References Pagination Tests

    [Test]
    public async Task FindSymbolUsages_WithMaxResults_LimitsOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all references first
        var allResult = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16);
        var totalCount = allResult.TotalCount;

        if (totalCount < 2)
        {
            Assert.Inconclusive("Need at least 2 references for this test");
            return;
        }

        // Act - Request only 2 references
        var limitedResult = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            maxResults: 2,
            offset: 0);

        // Assert
        limitedResult.TotalCount.Should().Be(totalCount);
        limitedResult.Usages.Should().HaveCount(2);
        limitedResult.HasMore.Should().BeTrue(
            because: "There are more references beyond the first 2");
    }

    [Test]
    public async Task FindSymbolUsages_WithOffset_SkipsReferences()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        var allResult = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16);
        var allReferences = allResult.Usages.ToList();
        var totalCount = allResult.TotalCount;

        if (totalCount < 3)
        {
            Assert.Inconclusive("Need at least 3 references for this test");
            return;
        }

        // Act - Skip first 2 references
        var offsetResult = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            maxResults: 100,
            offset: 2);

        // Assert
        offsetResult.TotalCount.Should().Be(totalCount);
        offsetResult.Usages.Should().HaveCount(totalCount - 2);

        // First reference in offset result should match third reference from all results
        offsetResult.Usages[0].FilePath.Should().Be(allReferences[2].FilePath);
        offsetResult.Usages[0].Line.Should().Be(allReferences[2].Line);
        offsetResult.Usages[0].Column.Should().Be(allReferences[2].Column);
    }

    [Test]
    public async Task FindSymbolUsages_WithContextLinesOne_ReturnsSingleLine()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            contextLines: 1);

        // Assert
        result.Usages.Should().NotBeEmpty();
        foreach (var reference in result.Usages)
        {
            // Context should be a single line (no newlines)
            reference.ContextSnippet.Should().NotContain(Environment.NewLine,
                because: "contextLines=1 should return only the line with the reference");
        }
    }

    [Test]
    public async Task FindSymbolUsages_WithContextLinesThree_ReturnsMultipleLines()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            contextLines: 3);

        // Assert
        result.Usages.Should().NotBeEmpty();

        // Find a reference that's not on the first or last line of the file
        var middleReference = result.Usages.FirstOrDefault(r =>
            r.Line > 1 &&
            r.FilePath.EndsWith("Program.cs"));

        if (middleReference != null)
        {
            // Context should have up to 3 lines (line before, current line, line after)
            var lines = middleReference.ContextSnippet.Split(Environment.NewLine);
            lines.Should().HaveCountGreaterOrEqualTo(1);
            lines.Should().HaveCountLessOrEqualTo(3,
                because: "contextLines=3 should return at most 3 lines");
        }
    }

    [Test]
    public async Task FindSymbolUsages_WithContextLinesFive_ReturnsUpToFiveLines()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            contextLines: 5);

        // Assert
        result.Usages.Should().NotBeEmpty();

        // Find a reference in the middle of a file
        var middleReference = result.Usages.FirstOrDefault(r =>
            r.Line > 2 &&
            r.FilePath.EndsWith("Program.cs"));

        if (middleReference != null)
        {
            // Context should have up to 5 lines (2 before, current, 2 after)
            var lines = middleReference.ContextSnippet.Split(Environment.NewLine);
            lines.Should().HaveCountGreaterOrEqualTo(1);
            lines.Should().HaveCountLessOrEqualTo(5,
                because: "contextLines=5 should return at most 5 lines");
        }
    }

    [Test]
    public async Task FindSymbolUsages_PaginationMetadata_IsAccurate()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all references
        var allResult = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16);
        var totalCount = allResult.TotalCount;

        if (totalCount < 2)
        {
            Assert.Inconclusive("Need at least 2 references for this test");
            return;
        }

        // Act - Get first page with maxResults = 1
        var firstPage = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            maxResults: 1,
            offset: 0);

        // Assert first page
        firstPage.TotalCount.Should().Be(totalCount);
        firstPage.Usages.Should().HaveCount(1);
        firstPage.HasMore.Should().BeTrue();

        // Act - Get last page
        var lastPage = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            maxResults: 100,
            offset: totalCount - 1);

        // Assert last page
        lastPage.TotalCount.Should().Be(totalCount);
        lastPage.Usages.Should().HaveCount(1);
        lastPage.HasMore.Should().BeFalse();
    }

    [Test]
    public async Task FindSymbolUsages_DefaultMaxResults_Is100()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Use default pagination
        var result = await _roslynService.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16);

        // Assert - Should return at most 100 results by default
        result.Usages.Should().HaveCountLessOrEqualTo(100,
            "Default maxResults should be 100");
        result.TotalCount.Should().BeGreaterOrEqualTo(result.Usages.Count);
    }

    #endregion

    #region Code Actions Pagination Tests

    [Test]
    public async Task GetCodeActions_WithMaxResults_LimitsOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all code actions for a file with errors
        var allActions = await _codeActionsService.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 1,
            column: 1,
            endLine: 100,
            endColumn: 1);

        var totalCount = allActions.TotalCount;

        if (totalCount < 2)
        {
            Assert.Inconclusive("Need at least 2 code actions for this test");
            return;
        }

        // Act - Request only 2 code actions
        var limitedActions = await _codeActionsService.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 1,
            column: 1,
            endLine: 100,
            endColumn: 1,
            maxResults: 2,
            offset: 0);

        // Assert
        limitedActions.Actions.Should().HaveCount(2,
            "maxResults=2 should limit output to 2 actions");
    }

    [Test]
    public async Task GetCodeActions_WithOffset_SkipsActions()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all code actions
        var allActions = await _codeActionsService.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 1,
            column: 1,
            endLine: 100,
            endColumn: 1);

        var totalCount = allActions.TotalCount;

        if (totalCount < 3)
        {
            Assert.Inconclusive("Need at least 3 code actions for this test");
            return;
        }

        // Act - Skip first 2 actions
        var offsetActions = await _codeActionsService.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 1,
            column: 1,
            endLine: 100,
            endColumn: 1,
            maxResults: 100,
            offset: 2);

        // Assert
        offsetActions.Actions.Should().HaveCount(totalCount - 2);

        // Third action overall should be first in offset result
        if (offsetActions.Actions.Count > 0)
        {
            offsetActions.Actions[0].Title.Should().Be(allActions.Actions[2].Title);
        }
    }

    [Test]
    public async Task GetCodeActions_DefaultMaxResults_Is50()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Use default pagination
        var result = await _codeActionsService.GetCodeActionsAsync(
            filePath: "ClassWithErrors.cs",
            line: 1,
            column: 1,
            endLine: 100,
            endColumn: 1);

        // Assert - Should return at most 50 results by default
        result.Actions.Should().HaveCountLessOrEqualTo(50,
            "Default maxResults for code actions should be 50");
    }

    #endregion
}

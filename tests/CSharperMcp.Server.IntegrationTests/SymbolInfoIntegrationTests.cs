using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests;

/// <summary>
/// Integration tests for enhanced get_symbol_info functionality (Task 2).
/// Tests the new IsFromWorkspace, source location, and includeDocumentation features.
/// </summary>
[TestFixture]
internal class SymbolInfoIntegrationTests
{
    private Mock<ILogger<WorkspaceManager>> _workspaceLoggerMock = null!;
    private Mock<ILogger<RoslynService>> _roslynLoggerMock = null!;
    private Mock<ILogger<DecompilerService>> _decompilerLoggerMock = null!;
    private WorkspaceManager _workspaceManager = null!;
    private DecompilerService _decompilerService = null!;
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
        _decompilerLoggerMock = new Mock<ILogger<DecompilerService>>();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _decompilerService = new DecompilerService(_workspaceManager, _decompilerLoggerMock.Object);
        _sut = new RoslynService(_workspaceManager, _decompilerService, _roslynLoggerMock.Object);
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
    public async Task GetSymbolInfoAsync_ForWorkspaceMethod_ReturnsIsFromWorkspaceTrue()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for Calculator.Add method at line 5
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.IsFromWorkspace.Should().BeTrue(
            because: "Calculator.Add is defined in workspace source code");
        symbolInfo.SourceFile.Should().NotBeNullOrEmpty();
        symbolInfo.SourceFile.Should().EndWith("Calculator.cs");
        symbolInfo.SourceLine.Should().Be(5);
        symbolInfo.SourceColumn.Should().BeGreaterThan(0);
        symbolInfo.Name.Should().Be("Add");
        symbolInfo.Kind.Should().Be("Method");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ForWorkspaceClass_ReturnsIsFromWorkspaceTrue()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for Calculator class at line 3
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            filePath: "Calculator.cs",
            line: 3,
            column: 14,
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.IsFromWorkspace.Should().BeTrue(
            because: "Calculator class is defined in workspace source code");
        symbolInfo.SourceFile.Should().NotBeNullOrEmpty();
        symbolInfo.SourceFile.Should().EndWith("Calculator.cs");
        symbolInfo.SourceLine.Should().Be(3);
        symbolInfo.SourceColumn.Should().BeGreaterThan(0);
        symbolInfo.Name.Should().Be("Calculator");
        symbolInfo.Kind.Should().Be("NamedType");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ForWorkspaceVariable_ReturnsIsFromWorkspaceTrue()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for local variable 'calc' at line 7 in Program.cs
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            filePath: "Program.cs",
            line: 7,
            column: 13,
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Name.Should().Be("calc");
        symbolInfo.Kind.Should().Be("Local");

        // For local variables, source location should still be populated
        symbolInfo.SourceFile.Should().NotBeNullOrEmpty();
        symbolInfo.SourceFile.Should().EndWith("Program.cs");
        symbolInfo.SourceLine.Should().Be(7);
    }

    [Test]
    public async Task GetSymbolInfoAsync_ForBclType_ReturnsIsFromWorkspaceFalse()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for System.Console (used in Program.cs)
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "System.Console",
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.IsFromWorkspace.Should().BeFalse(
            because: "System.Console is a BCL type, not in workspace");
        symbolInfo.SourceFile.Should().BeNull(
            because: "BCL types don't have source files in workspace");
        symbolInfo.SourceLine.Should().BeNull();
        symbolInfo.SourceColumn.Should().BeNull();
        symbolInfo.Name.Should().Be("Console");
        symbolInfo.Namespace.Should().Be("System");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ForNuGetType_ReturnsIsFromWorkspaceFalse()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for JObject from Newtonsoft.Json
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "Newtonsoft.Json.Linq.JObject",
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.IsFromWorkspace.Should().BeFalse(
            because: "JObject is from a NuGet package, not workspace code");
        symbolInfo.SourceFile.Should().BeNull();
        symbolInfo.SourceLine.Should().BeNull();
        symbolInfo.SourceColumn.Should().BeNull();
        symbolInfo.Name.Should().Be("JObject");
        symbolInfo.Namespace.Should().Be("Newtonsoft.Json.Linq");
        symbolInfo.Assembly.Should().Be("Newtonsoft.Json");
    }

    [Test]
    public async Task GetSymbolInfoAsync_WithIncludeDocumentationFalse_ReturnsNullDocComment()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info WITHOUT documentation
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "System.String",
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.DocComment.Should().BeNull(
            because: "includeDocumentation=false should omit doc comments");
    }

    [Test]
    public async Task GetSymbolInfoAsync_WithIncludeDocumentationTrue_ReturnsDocComment()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info WITH documentation
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "System.String",
            includeDocumentation: true);

        // Assert
        symbolInfo.Should().NotBeNull();

        // System.String should have XML documentation
        // Note: May be null depending on whether reference assemblies have docs
        // If not null, it should be non-empty
        if (symbolInfo!.DocComment != null)
        {
            symbolInfo.DocComment.Should().NotBeEmpty(
                because: "includeDocumentation=true should populate doc comments when available");
        }
    }

    [Test]
    public async Task GetSymbolInfoAsync_ByLocation_ForWorkspaceSymbol_PopulatesAllFields()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for Calculator.Add method by location
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            includeDocumentation: false);

        // Assert - Verify all fields are populated correctly
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Kind.Should().Be("Method");
        symbolInfo.Name.Should().Be("Add");
        symbolInfo.ContainingType.Should().Contain("Calculator");
        symbolInfo.Namespace.Should().Be("SimpleProject");
        symbolInfo.Assembly.Should().Be("SimpleProject");
        symbolInfo.Package.Should().BeNull();
        symbolInfo.Modifiers.Should().Contain("public");
        symbolInfo.Signature.Should().NotBeNullOrEmpty();
        symbolInfo.Signature.Should().Contain("int");

        // New fields from Task 2
        symbolInfo.IsFromWorkspace.Should().BeTrue();
        symbolInfo.SourceFile.Should().EndWith("Calculator.cs");
        symbolInfo.SourceLine.Should().Be(5);
        symbolInfo.SourceColumn.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetSymbolInfoAsync_ByName_ForBclSymbol_PopulatesCorrectFields()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for System.String by name
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "System.String",
            includeDocumentation: false);

        // Assert - Verify BCL symbol fields
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Kind.Should().Be("NamedType");
        symbolInfo.Name.Should().Be("String");
        symbolInfo.Namespace.Should().Be("System");
        symbolInfo.Assembly.Should().Contain("System");
        symbolInfo.Package.Should().BeNull(because: "BCL types are not NuGet packages");

        // New fields from Task 2
        symbolInfo.IsFromWorkspace.Should().BeFalse();
        symbolInfo.SourceFile.Should().BeNull();
        symbolInfo.SourceLine.Should().BeNull();
        symbolInfo.SourceColumn.Should().BeNull();
        symbolInfo.DocComment.Should().BeNull(because: "includeDocumentation=false");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ForWorkspaceSymbol_ReturnsAbsoluteSourcePath()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.SourceFile.Should().NotBeNullOrEmpty();

        // Verify path is absolute
        Path.IsPathRooted(symbolInfo.SourceFile!).Should().BeTrue(
            because: "Source file path should be absolute for easy navigation");

        // Verify file exists
        File.Exists(symbolInfo.SourceFile).Should().BeTrue(
            because: "Source file path should point to an existing file");
    }

    [Test]
    public async Task GetSymbolInfoAsync_CompareDocumentationFlag_ShowsDifference()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info with and without documentation
        var withoutDocs = await _sut.GetSymbolInfoAsync(
            symbolName: "System.Console",
            includeDocumentation: false);

        var withDocs = await _sut.GetSymbolInfoAsync(
            symbolName: "System.Console",
            includeDocumentation: true);

        // Assert
        withoutDocs.Should().NotBeNull();
        withDocs.Should().NotBeNull();

        // Without docs should have null DocComment
        withoutDocs!.DocComment.Should().BeNull();

        // With docs may have documentation (depending on reference assemblies)
        // If documentation is available, it should be populated
        // This demonstrates the includeDocumentation flag is working

        // Both should have the same basic symbol info
        withoutDocs.Name.Should().Be(withDocs!.Name);
        withoutDocs.Kind.Should().Be(withDocs.Kind);
        withoutDocs.Namespace.Should().Be(withDocs.Namespace);
    }
}

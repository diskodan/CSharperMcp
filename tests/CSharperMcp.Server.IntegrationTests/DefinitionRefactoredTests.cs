using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests;

/// <summary>
/// Integration tests for refactored get_definition tool (Task 6).
/// Verifies that get_definition returns consistent response structure without decompilation.
/// </summary>
[TestFixture]
internal class DefinitionRefactoredTests
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

    #region Workspace Symbol Tests

    [Test]
    public async Task GetDefinition_ForWorkspaceMethod_ReturnsSourceLocation()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition at Calculator.Add method usage (Program.cs line 10)
        var definition = await _sut.GetDefinitionAsync(
            filePath: "Program.cs",
            line: 10,
            column: 27);

        // Assert - Should return source location without decompilation
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeTrue();
        definition.FilePath.Should().NotBeNullOrEmpty();
        definition.FilePath.Should().EndWith("Calculator.cs");
        definition.Line.Should().Be(5); // Add method is on line 5
        definition.Column.Should().BeGreaterThan(0);
        definition.Assembly.Should().Be("SimpleProject");

        // DLL-only fields should be null for workspace symbols
        definition.TypeName.Should().BeNull();
        definition.SymbolKind.Should().BeNull();
        definition.Signature.Should().BeNull();
        definition.Package.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_ForWorkspaceClass_ReturnsSourceLocation()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for Calculator type at instantiation (Program.cs line 7)
        var definition = await _sut.GetDefinitionAsync(
            filePath: "Program.cs",
            line: 7,
            column: 24);

        // Assert - Should return source location
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeTrue();
        definition.FilePath.Should().EndWith("Calculator.cs");
        definition.Line.Should().Be(3); // Calculator class starts at line 3
        definition.Column.Should().BeGreaterThan(0);
        definition.Assembly.Should().Be("SimpleProject");

        // DLL-only fields should be null
        definition.TypeName.Should().BeNull();
        definition.SymbolKind.Should().BeNull();
        definition.Signature.Should().BeNull();
        definition.Package.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_ForWorkspaceSymbol_ReturnsAbsolutePath()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var definition = await _sut.GetDefinitionAsync(
            filePath: "Program.cs",
            line: 10,
            column: 27);

        // Assert
        definition.Should().NotBeNull();
        definition!.FilePath.Should().NotBeNullOrEmpty();

        // Verify path is absolute
        Path.IsPathRooted(definition.FilePath!).Should().BeTrue(
            because: "File paths should be absolute for easy navigation");

        // Verify file exists
        File.Exists(definition.FilePath).Should().BeTrue(
            because: "Definition path should point to an existing file");
    }

    #endregion

    #region BCL Symbol Tests

    [Test]
    public async Task GetDefinition_ForBclType_ReturnsMetadataOnly()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for System.Console by name
        var definition = await _sut.GetDefinitionAsync(symbolName: "System.Console");

        // Assert - Should return metadata without decompilation
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();
        definition.Assembly.Should().Contain("System");
        definition.TypeName.Should().Contain("Console");
        definition.SymbolKind.Should().NotBeNullOrEmpty();
        definition.SymbolKind.Should().Be("NamedType");
        definition.Signature.Should().NotBeNullOrEmpty();
        definition.Package.Should().BeNull(because: "BCL types are not NuGet packages");

        // Workspace-only fields should be null
        definition.FilePath.Should().BeNull(
            because: "BCL types don't have source files in workspace");
        definition.Line.Should().BeNull();
        definition.Column.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_ForBclType_DoesNotDecompile()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for System.String (would be 3000+ lines if decompiled)
        var definition = await _sut.GetDefinitionAsync(symbolName: "System.String");

        // Assert - Should NOT contain decompiled source
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();
        definition.TypeName.Should().Contain("String");
        definition.SymbolKind.Should().Be("NamedType");

        // Should have brief signature, not full decompiled source
        definition.Signature.Should().NotBeNullOrEmpty();
        definition.Signature!.Length.Should().BeLessThan(500,
            because: "Signature should be brief, not full decompiled source");

        // Verify no source location
        definition.FilePath.Should().BeNull();
        definition.Line.Should().BeNull();
        definition.Column.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_ForBclMethod_ReturnsMetadata()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for Console.WriteLine (used in Program.cs line 14)
        var definition = await _sut.GetDefinitionAsync(
            filePath: "Program.cs",
            line: 14,
            column: 17);

        // Assert
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();
        definition.Assembly.Should().Contain("System");
        definition.TypeName.Should().NotBeNullOrEmpty();
        definition.SymbolKind.Should().NotBeNullOrEmpty();
        definition.Signature.Should().NotBeNullOrEmpty();

        // Should not have source location
        definition.FilePath.Should().BeNull();
        definition.Line.Should().BeNull();
        definition.Column.Should().BeNull();
    }

    #endregion

    #region NuGet Package Tests

    [Test]
    public async Task GetDefinition_ForNuGetType_ReturnsMetadataOnly()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for JObject from Newtonsoft.Json
        var definition = await _sut.GetDefinitionAsync(
            symbolName: "Newtonsoft.Json.Linq.JObject");

        // Assert - Should return metadata without decompilation
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();
        definition.Assembly.Should().Be("Newtonsoft.Json");
        definition.TypeName.Should().Contain("JObject");
        definition.SymbolKind.Should().Be("NamedType");
        definition.Signature.Should().NotBeNullOrEmpty();
        definition.Package.Should().Be("Newtonsoft.Json");

        // Should not have source location
        definition.FilePath.Should().BeNull();
        definition.Line.Should().BeNull();
        definition.Column.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_ForNuGetType_DoesNotDecompile()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for JObject (would be hundreds of lines if decompiled)
        var definition = await _sut.GetDefinitionAsync(
            symbolName: "Newtonsoft.Json.Linq.JObject");

        // Assert - Should NOT contain decompiled source
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();

        // Should have brief signature, not full decompiled source
        definition.Signature.Should().NotBeNullOrEmpty();
        definition.Signature!.Length.Should().BeLessThan(500,
            because: "Signature should be brief, not full decompiled source");
    }

    #endregion

    #region Response Structure Consistency Tests

    [Test]
    public async Task GetDefinition_WorkspaceVsBcl_HasConsistentStructure()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get both workspace and BCL definitions
        var workspaceDefinition = await _sut.GetDefinitionAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16);

        var bclDefinition = await _sut.GetDefinitionAsync(
            symbolName: "System.Console");

        // Assert - Both should have consistent structure
        workspaceDefinition.Should().NotBeNull();
        bclDefinition.Should().NotBeNull();

        // Both should have IsFromWorkspace flag
        workspaceDefinition!.IsFromWorkspace.Should().BeTrue();
        bclDefinition!.IsFromWorkspace.Should().BeFalse();

        // Both should have Assembly
        workspaceDefinition.Assembly.Should().NotBeNullOrEmpty();
        bclDefinition.Assembly.Should().NotBeNullOrEmpty();

        // Workspace should have file location, BCL should not
        workspaceDefinition.FilePath.Should().NotBeNullOrEmpty();
        workspaceDefinition.Line.Should().NotBeNull();
        workspaceDefinition.Column.Should().NotBeNull();

        bclDefinition.FilePath.Should().BeNull();
        bclDefinition.Line.Should().BeNull();
        bclDefinition.Column.Should().BeNull();

        // BCL should have metadata fields, workspace should not
        bclDefinition.TypeName.Should().NotBeNullOrEmpty();
        bclDefinition.SymbolKind.Should().NotBeNullOrEmpty();
        bclDefinition.Signature.Should().NotBeNullOrEmpty();

        workspaceDefinition.TypeName.Should().BeNull();
        workspaceDefinition.SymbolKind.Should().BeNull();
        workspaceDefinition.Signature.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_BclVsNuGet_HasConsistentStructure()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get both BCL and NuGet definitions
        var bclDefinition = await _sut.GetDefinitionAsync(
            symbolName: "System.String");

        var nugetDefinition = await _sut.GetDefinitionAsync(
            symbolName: "Newtonsoft.Json.Linq.JObject");

        // Assert - Both should have consistent structure
        bclDefinition.Should().NotBeNull();
        nugetDefinition.Should().NotBeNull();

        // Both should be non-workspace
        bclDefinition!.IsFromWorkspace.Should().BeFalse();
        nugetDefinition!.IsFromWorkspace.Should().BeFalse();

        // Both should have metadata fields
        bclDefinition.TypeName.Should().NotBeNullOrEmpty();
        bclDefinition.SymbolKind.Should().NotBeNullOrEmpty();
        bclDefinition.Signature.Should().NotBeNullOrEmpty();

        nugetDefinition.TypeName.Should().NotBeNullOrEmpty();
        nugetDefinition.SymbolKind.Should().NotBeNullOrEmpty();
        nugetDefinition.Signature.Should().NotBeNullOrEmpty();

        // Neither should have source location
        bclDefinition.FilePath.Should().BeNull();
        bclDefinition.Line.Should().BeNull();
        bclDefinition.Column.Should().BeNull();

        nugetDefinition.FilePath.Should().BeNull();
        nugetDefinition.Line.Should().BeNull();
        nugetDefinition.Column.Should().BeNull();

        // Package field should differentiate them
        bclDefinition.Package.Should().BeNull();
        nugetDefinition.Package.Should().Be("Newtonsoft.Json");
    }

    #endregion

    #region Error Cases

    [Test]
    public async Task GetDefinition_WithInvalidSymbol_ReturnsNull()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var definition = await _sut.GetDefinitionAsync(
            filePath: "NonExistent.cs",
            line: 1,
            column: 1);

        // Assert
        definition.Should().BeNull();
    }

    [Test]
    public async Task GetDefinition_WithInvalidSymbolName_ReturnsNull()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var definition = await _sut.GetDefinitionAsync(
            symbolName: "NonExistent.FakeType");

        // Assert
        definition.Should().BeNull();
    }

    #endregion
}

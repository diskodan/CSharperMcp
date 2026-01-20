using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests;

/// <summary>
/// Integration tests for updated get_type_members tool (Task 8).
/// Tests the includeImplementation parameter and lineCount accuracy.
/// </summary>
[TestFixture]
internal class TypeMembersDetailTests
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

    #region Workspace Type Tests

    [Test]
    public async Task GetTypeMembers_WorkspaceType_SignaturesOnly_ReturnsCompactOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get signatures only
        var result = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Calculator");
        result.Namespace.Should().Be("SimpleProject");
        result.IsFromWorkspace.Should().BeTrue();
        result.IncludesImplementation.Should().BeFalse();
        result.LineCount.Should().BeGreaterThan(0);
        result.SourceCode.Should().NotBeNullOrEmpty();

        // Should contain method signatures
        result.SourceCode.Should().Contain("int Add");
        result.SourceCode.Should().Contain("int Subtract");
        result.SourceCode.Should().Contain("int Multiply");
        result.SourceCode.Should().Contain("double Divide");

        // Should not contain implementation details
        result.SourceCode.Should().NotContain("=> a + b");
        result.SourceCode.Should().NotContain("=> a - b");
        result.SourceCode.Should().NotContain("throw new DivideByZeroException");
    }

    [Test]
    public async Task GetTypeMembers_WorkspaceType_FullImplementation_ReturnsDetailedOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get full implementation
        var result = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Calculator");
        result.Namespace.Should().Be("SimpleProject");
        result.IsFromWorkspace.Should().BeTrue();
        result.IncludesImplementation.Should().BeTrue();
        result.LineCount.Should().BeGreaterThan(0);
        result.SourceCode.Should().NotBeNullOrEmpty();

        // Should contain full method implementations
        result.SourceCode.Should().Contain("=> a + b");
        result.SourceCode.Should().Contain("=> a - b");
        result.SourceCode.Should().Contain("=> a * b");
        result.SourceCode.Should().Contain("throw new DivideByZeroException");
    }

    [Test]
    public async Task GetTypeMembers_WorkspaceType_CompareModes_VerifyDifference()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var signaturesOnly = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: false);

        var fullImpl = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImpl.Should().NotBeNull();

        signaturesOnly!.IncludesImplementation.Should().BeFalse();
        fullImpl!.IncludesImplementation.Should().BeTrue();

        // Both should have same basic info
        signaturesOnly.TypeName.Should().Be(fullImpl.TypeName);
        signaturesOnly.Namespace.Should().Be(fullImpl.Namespace);

        // Signatures-only should be smaller or equal
        signaturesOnly.LineCount.Should().BeLessOrEqualTo(fullImpl.LineCount);

        // Verify content difference
        signaturesOnly.SourceCode.Should().NotContain("=> a + b");
        fullImpl.SourceCode.Should().Contain("=> a + b");
    }

    #endregion

    #region BCL Type Tests

    [Test]
    public async Task GetTypeMembers_BclType_SignaturesOnly_ReturnsCompactOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get System.Console signatures only
        var result = await _sut.GetTypeMembersAsync(
            "System.Console",
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Console");
        result.Namespace.Should().Be("System");
        result.IsFromWorkspace.Should().BeFalse();
        result.IncludesImplementation.Should().BeFalse();
        result.LineCount.Should().BeGreaterThan(0);
        result.SourceCode.Should().NotBeNullOrEmpty();

        // Should contain method signatures
        result.SourceCode.Should().Contain("WriteLine");
    }

    [Test]
    public async Task GetTypeMembers_BclType_FullImplementation_ReturnsDetailedOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get System.Console full implementation
        var result = await _sut.GetTypeMembersAsync(
            "System.Console",
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Console");
        result.Namespace.Should().Be("System");
        result.IsFromWorkspace.Should().BeFalse();
        result.IncludesImplementation.Should().BeTrue();
        result.LineCount.Should().BeGreaterThan(0);
        result.SourceCode.Should().NotBeNullOrEmpty();

        // Should contain method implementations
        result.SourceCode.Should().Contain("WriteLine");
    }

    [Test]
    public async Task GetTypeMembers_BclType_CompareModes_VerifyDifference()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var signaturesOnly = await _sut.GetTypeMembersAsync(
            "System.Console",
            includeImplementation: false);

        var fullImpl = await _sut.GetTypeMembersAsync(
            "System.Console",
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImpl.Should().NotBeNull();

        signaturesOnly!.IncludesImplementation.Should().BeFalse();
        fullImpl!.IncludesImplementation.Should().BeTrue();

        // Both should have same basic info
        signaturesOnly.TypeName.Should().Be(fullImpl.TypeName);
        signaturesOnly.Namespace.Should().Be(fullImpl.Namespace);

        // The includeImplementation flag should be correctly set
        signaturesOnly.IncludesImplementation.Should().NotBe(fullImpl.IncludesImplementation);
    }

    [Test]
    public async Task GetTypeMembers_SystemString_SignaturesOnly_IsCompact()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var signaturesOnly = await _sut.GetTypeMembersAsync(
            "System.String",
            includeImplementation: false);

        var fullImpl = await _sut.GetTypeMembersAsync(
            "System.String",
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImpl.Should().NotBeNull();

        // Signatures-only should be significantly smaller
        // Note: Includes XML documentation which adds ~200-300 lines
        signaturesOnly!.LineCount.Should().BeLessThan(1000,
            because: "Signatures-only should be compact for System.String");

        fullImpl!.LineCount.Should().BeGreaterThan(1000,
            because: "Full implementation should be large for System.String");

        // Verify size difference (signatures should be at least 20% smaller)
        signaturesOnly.LineCount.Should().BeLessThan(fullImpl.LineCount * 4 / 5);
    }

    #endregion

    #region LineCount Accuracy Tests

    [Test]
    public async Task GetTypeMembers_LineCount_IsAccurate()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        var actualLineCount = result!.SourceCode.Split('\n').Length;
        result.LineCount.Should().Be(actualLineCount,
            because: "LineCount should match actual line count in source code");
    }

    [Test]
    public async Task GetTypeMembers_LineCount_ReflectsDetailLevel()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var signaturesOnly = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: false);

        var fullImpl = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImpl.Should().NotBeNull();

        // LineCount should reflect the actual difference in output size
        signaturesOnly!.LineCount.Should().BeLessOrEqualTo(fullImpl!.LineCount,
            because: "Signatures-only should have fewer or equal lines than full implementation");
    }

    #endregion

    #region NuGet Package Tests

    [Test]
    public async Task GetTypeMembers_NuGetType_SignaturesOnly_ReturnsCompactOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetTypeMembersAsync(
            "Newtonsoft.Json.Linq.JObject",
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("JObject");
        result.Namespace.Should().Be("Newtonsoft.Json.Linq");
        result.IsFromWorkspace.Should().BeFalse();
        result.Package.Should().Be("Newtonsoft.Json");
        result.IncludesImplementation.Should().BeFalse();
        result.LineCount.Should().BeGreaterThan(0);
        result.SourceCode.Should().NotBeNullOrEmpty();
        result.SourceCode.Should().Contain("JObject");
    }

    [Test]
    public async Task GetTypeMembers_NuGetType_FullImplementation_ReturnsDetailedOutput()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetTypeMembersAsync(
            "Newtonsoft.Json.Linq.JObject",
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("JObject");
        result.Namespace.Should().Be("Newtonsoft.Json.Linq");
        result.IsFromWorkspace.Should().BeFalse();
        result.Package.Should().Be("Newtonsoft.Json");
        result.IncludesImplementation.Should().BeTrue();
        result.LineCount.Should().BeGreaterThan(0);
        result.SourceCode.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task GetTypeMembers_NuGetType_CompareModes_VerifyDifference()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var signaturesOnly = await _sut.GetTypeMembersAsync(
            "Newtonsoft.Json.Linq.JObject",
            includeImplementation: false);

        var fullImpl = await _sut.GetTypeMembersAsync(
            "Newtonsoft.Json.Linq.JObject",
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImpl.Should().NotBeNull();

        signaturesOnly!.IncludesImplementation.Should().BeFalse();
        fullImpl!.IncludesImplementation.Should().BeTrue();

        // Signatures-only should be significantly smaller (but XML docs add overhead)
        signaturesOnly.LineCount.Should().BeLessThan(fullImpl.LineCount * 4 / 5,
            because: "Signatures-only should be at least 20% smaller than full implementation");
    }

    #endregion

    #region Response Structure Tests

    [Test]
    public async Task GetTypeMembers_AllFields_ArePopulated()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator",
            includeImplementation: true);

        // Assert - Verify all fields are populated
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Calculator");
        result.Namespace.Should().Be("SimpleProject");
        result.Assembly.Should().Be("SimpleProject");
        result.Package.Should().BeNull();
        result.IsFromWorkspace.Should().BeTrue();
        result.FilePath.Should().NotBeNullOrEmpty();
        result.SourceCode.Should().NotBeNullOrEmpty();
        result.IncludesImplementation.Should().BeTrue();
        result.LineCount.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetTypeMembers_BclType_AllFields_ArePopulated()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetTypeMembersAsync(
            "System.Console",
            includeImplementation: false);

        // Assert - Verify all fields are populated
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Console");
        result.Namespace.Should().Be("System");
        result.Assembly.Should().NotBeNullOrEmpty();
        result.Package.Should().BeNull();
        result.IsFromWorkspace.Should().BeFalse();
        result.FilePath.Should().BeNull(because: "BCL types don't have workspace files");
        result.SourceCode.Should().NotBeNullOrEmpty();
        result.IncludesImplementation.Should().BeFalse();
        result.LineCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Error Cases

    [Test]
    public async Task GetTypeMembers_InvalidType_ReturnsNull()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.GetTypeMembersAsync(
            "NonExistent.Type",
            includeImplementation: false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Default Parameter Tests

    [Test]
    public async Task GetTypeMembers_DefaultIncludeImplementation_IsTrue()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Call with default includeImplementation (should be true)
        var result = await _sut.GetTypeMembersAsync(
            "SimpleProject.Calculator");

        // Assert
        result.Should().NotBeNull();
        result!.IncludesImplementation.Should().BeTrue(
            because: "Default includeImplementation should be true for backward compatibility");

        // Should contain implementation
        result.SourceCode.Should().Contain("=> a + b");
    }

    #endregion
}

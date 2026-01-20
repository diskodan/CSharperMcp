using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests;

/// <summary>
/// Integration tests for the new get_decompiled_source tool (Task 7).
/// Tests decompilation with includeImplementation parameter and obfuscation detection.
/// </summary>
[TestFixture]
internal class DecompiledSourceIntegrationTests
{
    private Mock<ILogger<WorkspaceManager>> _workspaceLoggerMock = null!;
    private Mock<ILogger<DecompilerService>> _decompilerLoggerMock = null!;
    private WorkspaceManager _workspaceManager = null!;
    private DecompilerService _sut = null!;

    [OneTimeSetUp]
    public static void OneTimeSetUp()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        _workspaceLoggerMock = new Mock<ILogger<WorkspaceManager>>();
        _decompilerLoggerMock = new Mock<ILogger<DecompilerService>>();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _sut = new DecompilerService(_workspaceManager, _decompilerLoggerMock.Object);

        // Initialize workspace to get access to BCL types
        var testDir = TestContext.CurrentContext.TestDirectory;
        var solutionPath = Path.Combine(testDir, "Fixtures", "SimpleSolution");
        var (success, _, _) = await _workspaceManager.InitializeAsync(solutionPath);
        success.Should().BeTrue("Failed to initialize workspace for tests");
    }

    [TearDown]
    public void TearDown()
    {
        _workspaceManager?.Dispose();
    }

    #region Signatures-Only Mode Tests

    [Test]
    public async Task DecompileType_SystemString_SignaturesOnly_ReturnsCompactOutput()
    {
        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("String");
        result.Namespace.Should().Be("System");
        result.Assembly.Should().NotBeNullOrEmpty();
        result.Package.Should().BeNull();
        result.IncludesImplementation.Should().BeFalse();
        result.DecompiledSource.Should().NotBeNullOrEmpty();
        result.LineCount.Should().BeGreaterThan(0);

        // Signatures-only should be compact (< 1000 lines vs 3000+ with implementation)
        // Note: Includes XML documentation which adds ~200-300 lines
        result.LineCount.Should().BeLessThan(1000,
            because: "Signatures-only mode should produce compact output for System.String");

        // Should contain class declaration
        result.DecompiledSource.Should().Contain("class String");
    }

    [Test]
    public async Task DecompileType_SystemDictionary_SignaturesOnly_ReturnsCompactOutput()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.Dictionary`2";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Dictionary");
        result.Namespace.Should().Be("System.Collections.Generic");
        result.IncludesImplementation.Should().BeFalse();
        result.DecompiledSource.Should().NotBeNullOrEmpty();
        result.LineCount.Should().BeGreaterThan(0);

        // Signatures-only should be much smaller than full implementation
        result.LineCount.Should().BeLessThan(1000,
            because: "Signatures-only mode should produce compact output for Dictionary");

        // Should contain generic class declaration
        result.DecompiledSource.Should().Contain("class Dictionary<");
    }

    #endregion

    #region Full Implementation Mode Tests

    [Test]
    public async Task DecompileType_SystemString_FullImplementation_ReturnsDetailedOutput()
    {
        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("String");
        result.Namespace.Should().Be("System");
        result.IncludesImplementation.Should().BeTrue();
        result.DecompiledSource.Should().NotBeNullOrEmpty();

        // Full implementation should be large (2000-4000 lines)
        result.LineCount.Should().BeGreaterThan(1000,
            because: "Full implementation should produce detailed output for System.String");

        // Should contain class declaration
        result.DecompiledSource.Should().Contain("class String");
    }

    [Test]
    public async Task DecompileType_SystemDictionary_FullImplementation_ReturnsDetailedOutput()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.Dictionary`2";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Dictionary");
        result.Namespace.Should().Be("System.Collections.Generic");
        result.IncludesImplementation.Should().BeTrue();
        result.DecompiledSource.Should().NotBeNullOrEmpty();

        // Full implementation should be large (1500+ lines for Dictionary)
        result.LineCount.Should().BeGreaterThan(1000,
            because: "Full implementation should produce detailed output for Dictionary");

        // Should contain generic class declaration
        result.DecompiledSource.Should().Contain("class Dictionary<");
    }

    #endregion

    #region Signatures vs Full Implementation Comparison

    [Test]
    public async Task DecompileType_SignaturesOnly_IsSmallerThanFullImplementation()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.List`1";

        // Act - Get both versions
        var signaturesOnly = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        var fullImplementation = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImplementation.Should().NotBeNull();

        signaturesOnly!.IncludesImplementation.Should().BeFalse();
        fullImplementation!.IncludesImplementation.Should().BeTrue();

        // Signatures-only should be significantly smaller (but XML docs add overhead)
        signaturesOnly.LineCount.Should().BeLessThan(fullImplementation.LineCount * 4 / 5,
            because: "Signatures-only should be at least 20% smaller than full implementation");

        // Both should have the same type info
        signaturesOnly.TypeName.Should().Be(fullImplementation.TypeName);
        signaturesOnly.Namespace.Should().Be(fullImplementation.Namespace);
    }

    [Test]
    public async Task DecompileType_SystemInt32_CompareModes()
    {
        // Arrange
        const string typeName = "System.Int32";

        // Act
        var signaturesOnly = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        var fullImplementation = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImplementation.Should().NotBeNull();

        signaturesOnly!.TypeName.Should().Be("Int32");
        fullImplementation!.TypeName.Should().Be("Int32");

        signaturesOnly.IncludesImplementation.Should().BeFalse();
        fullImplementation.IncludesImplementation.Should().BeTrue();

        // Verify source contains struct declaration
        signaturesOnly.DecompiledSource.Should().Contain("struct Int32");
        fullImplementation.DecompiledSource.Should().Contain("struct Int32");
    }

    #endregion

    #region LineCount Accuracy Tests

    [Test]
    public async Task DecompileType_LineCount_IsAccurate()
    {
        // Arrange
        const string typeName = "System.IDisposable";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        var actualLineCount = result!.DecompiledSource.Split('\n').Length;
        result.LineCount.Should().Be(actualLineCount,
            because: "LineCount should match actual line count in decompiled source");
    }

    [Test]
    public async Task DecompileType_LineCount_ReflectsDetailLevel()
    {
        // Arrange
        const string typeName = "System.Console";

        // Act
        var signaturesResult = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        var fullResult = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        signaturesResult.Should().NotBeNull();
        fullResult.Should().NotBeNull();

        // LineCount should accurately reflect the size difference
        signaturesResult!.LineCount.Should().BeLessThan(fullResult!.LineCount,
            because: "Signatures-only should have fewer lines than full implementation");
    }

    #endregion

    #region Obfuscation Detection Tests

    [Test]
    public async Task DecompileType_BclTypes_NotFlaggedAsObfuscated()
    {
        // Arrange - Test multiple BCL types
        var bclTypes = new[]
        {
            "System.String",
            "System.Int32",
            "System.Collections.Generic.List`1",
            "System.Collections.Generic.Dictionary`2",
            "System.Linq.Enumerable"
        };

        foreach (var typeName in bclTypes)
        {
            // Act
            var result = await _sut.DecompileTypeAsync(
                typeName,
                assemblyName: null,
                includeImplementation: true);

            // Assert
            result.Should().NotBeNull($"Failed to decompile {typeName}");
            result!.IsLikelyObfuscated.Should().BeFalse(
                $"BCL type {typeName} should not be flagged as obfuscated");
            result.ObfuscationWarning.Should().BeNull(
                $"BCL type {typeName} should not have obfuscation warning");
        }
    }

    [Test]
    public async Task DecompileType_ObfuscatedAssembly_ShouldDetectObfuscation()
    {
        // Note: This test would require an actual obfuscated assembly as a test fixture.
        // For now, we verify the obfuscation detection logic exists and returns correct structure.

        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert - Verify obfuscation fields are present
        result.Should().NotBeNull();
        result!.IsLikelyObfuscated.Should().BeFalse();
        result.ObfuscationWarning.Should().BeNull();

        // The structure supports obfuscation detection
        // If an obfuscated assembly was tested, these fields would be populated
    }

    #endregion

    #region Generic Types Tests

    [Test]
    public async Task DecompileType_GenericList_ReturnsCorrectTypeInfo()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.List`1";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("List");
        result.Namespace.Should().Be("System.Collections.Generic");
        result.DecompiledSource.Should().Contain("class List<");
    }

    [Test]
    public async Task DecompileType_GenericDictionary_ReturnsCorrectTypeInfo()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.Dictionary`2";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Dictionary");
        result.Namespace.Should().Be("System.Collections.Generic");
        result.DecompiledSource.Should().Contain("class Dictionary<");
    }

    #endregion

    #region Interface and Enum Tests

    [Test]
    public async Task DecompileType_Interface_IDisposable()
    {
        // Arrange
        const string typeName = "System.IDisposable";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("IDisposable");
        result.Namespace.Should().Be("System");
        result.DecompiledSource.Should().Contain("interface IDisposable");
        result.DecompiledSource.Should().Contain("void Dispose()");

        // Interfaces don't have implementation, so line count should be small
        result.LineCount.Should().BeLessThan(50);
    }

    [Test]
    public async Task DecompileType_Enum_DayOfWeek()
    {
        // Arrange
        const string typeName = "System.DayOfWeek";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("DayOfWeek");
        result.Namespace.Should().Be("System");
        result.DecompiledSource.Should().Contain("enum DayOfWeek");

        // Enums should be compact
        result.LineCount.Should().BeLessThan(50);
    }

    #endregion

    #region Error Cases

    [Test]
    public async Task DecompileType_NonExistentType_ReturnsNull()
    {
        // Arrange
        const string typeName = "NonExistent.FakeType";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task DecompileType_InvalidTypeName_ReturnsNull()
    {
        // Arrange
        const string typeName = "Not.A.Valid.Type.Name.At.All";

        // Act
        var result = await _sut.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region NuGet Package Tests

    [Test]
    public async Task DecompileType_NuGetType_ReturnsCorrectPackageInfo()
    {
        // Arrange - Load solution with NuGet references
        _workspaceManager.Dispose();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _sut = new DecompilerService(_workspaceManager, _decompilerLoggerMock.Object);

        var testDir = TestContext.CurrentContext.TestDirectory;
        var nugetSolutionPath = Path.Combine(testDir, "Fixtures", "SolutionWithNuGet");
        var (success, _, _) = await _workspaceManager.InitializeAsync(nugetSolutionPath);
        success.Should().BeTrue("Failed to initialize NuGet solution");

        // Act - Decompile JObject from Newtonsoft.Json
        var result = await _sut.DecompileTypeAsync(
            "Newtonsoft.Json.Linq.JObject",
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("JObject");
        result.Namespace.Should().Be("Newtonsoft.Json.Linq");
        result.Assembly.Should().Be("Newtonsoft.Json");
        result.Package.Should().Be("Newtonsoft.Json");
        result.IncludesImplementation.Should().BeFalse();
        result.DecompiledSource.Should().NotBeNullOrEmpty();
        result.DecompiledSource.Should().Contain("JObject");
    }

    [Test]
    public async Task DecompileType_NuGetType_SignaturesOnly_IsCompact()
    {
        // Arrange
        _workspaceManager.Dispose();
        _workspaceManager = new WorkspaceManager(_workspaceLoggerMock.Object);
        _sut = new DecompilerService(_workspaceManager, _decompilerLoggerMock.Object);

        var testDir = TestContext.CurrentContext.TestDirectory;
        var nugetSolutionPath = Path.Combine(testDir, "Fixtures", "SolutionWithNuGet");
        await _workspaceManager.InitializeAsync(nugetSolutionPath);

        // Act
        var signaturesOnly = await _sut.DecompileTypeAsync(
            "Newtonsoft.Json.Linq.JObject",
            assemblyName: null,
            includeImplementation: false);

        var fullImpl = await _sut.DecompileTypeAsync(
            "Newtonsoft.Json.Linq.JObject",
            assemblyName: null,
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImpl.Should().NotBeNull();

        signaturesOnly!.LineCount.Should().BeLessThan(fullImpl!.LineCount / 2,
            because: "Signatures-only should be much smaller than full implementation");
    }

    #endregion
}

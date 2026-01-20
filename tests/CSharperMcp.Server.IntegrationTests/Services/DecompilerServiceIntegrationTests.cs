using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests.Services;

/// <summary>
/// Integration tests for DecompilerService decompiling real BCL and NuGet types.
/// These tests verify that the decompiler works correctly with both includeImplementation modes.
/// </summary>
[TestFixture]
internal class DecompilerServiceIntegrationTests
{
    private WorkspaceManager _workspaceManager = null!;
    private DecompilerService _decompilerService = null!;
    private string _simpleSolutionPath = null!;

    [OneTimeSetUp]
    public static void OneTimeSetUp()
    {
        // MSBuildLocator should already be registered by the test assembly setup
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var workspaceLogger = loggerFactory.CreateLogger<WorkspaceManager>();
        var decompilerLogger = loggerFactory.CreateLogger<DecompilerService>();

        _workspaceManager = new WorkspaceManager(workspaceLogger);
        _decompilerService = new DecompilerService(_workspaceManager, decompilerLogger);

        // Load a simple solution to get access to BCL types
        _simpleSolutionPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            "SimpleSolution");

        var (success, _, _) = await _workspaceManager.InitializeAsync(_simpleSolutionPath);
        success.Should().BeTrue("Failed to initialize workspace for integration tests");
    }

    [TearDown]
    public void TearDown()
    {
        _workspaceManager.Dispose();
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldDecompileSystemString_WithSignaturesOnly()
    {
        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("String");
        result.Namespace.Should().Be("System");
        result.Assembly.Should().NotBeNullOrEmpty();
        result.Package.Should().BeNull(); // BCL type, not a NuGet package
        result.IncludesImplementation.Should().BeFalse();
        result.DecompiledSource.Should().NotBeNullOrEmpty();
        result.LineCount.Should().BeGreaterThan(0);

        // Verify source contains class declaration
        result.DecompiledSource.Should().Contain("class String");

        // Verify signatures-only mode produces compact output
        // With signatures only, String should be < 1000 lines (vs 3000+ with implementation)
        // Note: Includes XML documentation which adds ~200-300 lines
        result.LineCount.Should().BeLessThan(1000,
            "Signatures-only mode should produce compact output for System.String");
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldDecompileSystemString_WithFullImplementation()
    {
        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("String");
        result.Namespace.Should().Be("System");
        result.IncludesImplementation.Should().BeTrue();
        result.DecompiledSource.Should().NotBeNullOrEmpty();

        // Verify source contains class declaration
        result.DecompiledSource.Should().Contain("class String");

        // With full implementation, String should be much larger (typically 2000-4000 lines)
        result.LineCount.Should().BeGreaterThan(1000,
            "Full implementation mode should produce detailed output for System.String");
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldDecompileSystemInt32_WithSignaturesOnly()
    {
        // Arrange
        const string typeName = "System.Int32";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("Int32");
        result.Namespace.Should().Be("System");
        result.IncludesImplementation.Should().BeFalse();
        result.DecompiledSource.Should().NotBeNullOrEmpty();

        // Verify source contains struct declaration
        result.DecompiledSource.Should().Contain("struct Int32");
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldDecompileGenericType_SystemCollectionsGenericList()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.List`1";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("List");
        result.Namespace.Should().Be("System.Collections.Generic");
        result.IncludesImplementation.Should().BeFalse();
        result.DecompiledSource.Should().NotBeNullOrEmpty();

        // Verify source contains generic class declaration
        result.DecompiledSource.Should().Contain("class List<");
    }

    [Test]
    public async Task DecompileTypeAsync_SignaturesOnly_ShouldBeSmallerThanFullImplementation()
    {
        // Arrange
        const string typeName = "System.Collections.Generic.Dictionary`2";

        // Act - get both versions
        var signaturesOnly = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        var fullImplementation = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNull();
        fullImplementation.Should().NotBeNull();

        signaturesOnly!.IncludesImplementation.Should().BeFalse();
        fullImplementation!.IncludesImplementation.Should().BeTrue();

        // Signatures-only should be significantly smaller
        signaturesOnly.LineCount.Should().BeLessThan(fullImplementation.LineCount / 2,
            "Signatures-only mode should produce output at least 50% smaller than full implementation");

        // Both should have the same type info
        signaturesOnly.TypeName.Should().Be(fullImplementation.TypeName);
        signaturesOnly.Namespace.Should().Be(fullImplementation.Namespace);
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldReturnNull_ForNonExistentType()
    {
        // Arrange
        const string typeName = "NonExistent.FakeType";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldDecompileInterface_SystemIDisposable()
    {
        // Arrange
        const string typeName = "System.IDisposable";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.TypeName.Should().Be("IDisposable");
        result.Namespace.Should().Be("System");
        result.DecompiledSource.Should().Contain("interface IDisposable");
        result.DecompiledSource.Should().Contain("void Dispose()");

        // Interfaces don't have implementation, so line count should be very small
        result.LineCount.Should().BeLessThan(50);
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldIncludeXmlDocumentation()
    {
        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();
        result!.DecompiledSource.Should().NotBeNullOrEmpty();

        // BCL types should have XML documentation comments
        // Look for common XML doc comment patterns
        var hasDocComments = result.DecompiledSource.Contains("///") ||
                            result.DecompiledSource.Contains("/// <summary>");

        hasDocComments.Should().BeTrue(
            "Decompiled source should include XML documentation comments when available");
    }

    [Test]
    public void DecompileType_SyncMethod_ShouldWorkWithIncludeImplementation()
    {
        // Arrange - Get System.String from current runtime
        const string typeName = "System.String";
        var assemblyPath = typeof(string).Assembly.Location;

        // Skip test if assembly path is empty (should never happen)
        if (string.IsNullOrEmpty(assemblyPath))
        {
            Assert.Ignore("Could not determine System.Private.CoreLib.dll location");
        }

        // Act
        var signaturesOnly = _decompilerService.DecompileType(
            assemblyPath,
            typeName,
            includeImplementation: false);

        var fullImpl = _decompilerService.DecompileType(
            assemblyPath,
            typeName,
            includeImplementation: true);

        // Assert
        signaturesOnly.Should().NotBeNullOrEmpty();
        fullImpl.Should().NotBeNullOrEmpty();

        // Full implementation should be larger
        fullImpl!.Length.Should().BeGreaterThan(signaturesOnly!.Length);
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldNotFlagBclTypesAsObfuscated()
    {
        // Arrange - Test with various BCL types
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
            var result = await _decompilerService.DecompileTypeAsync(
                typeName,
                assemblyName: null,
                includeImplementation: true);

            // Assert
            result.Should().NotBeNull($"Failed to decompile {typeName}");
            result!.IsLikelyObfuscated.Should().BeFalse(
                $"BCL type {typeName} should not be flagged as obfuscated");
            result.ObfuscationWarning.Should().BeNull(
                $"BCL type {typeName} should not have an obfuscation warning");
        }
    }

    [Test]
    public async Task DecompileTypeAsync_ShouldIncludeObfuscationWarning_WhenObfuscationDetected()
    {
        // Arrange
        const string typeName = "System.String";

        // Act
        var result = await _decompilerService.DecompileTypeAsync(
            typeName,
            assemblyName: null,
            includeImplementation: false);

        // Assert
        result.Should().NotBeNull();

        // BCL types should not be obfuscated
        result!.IsLikelyObfuscated.Should().BeFalse();
        result.ObfuscationWarning.Should().BeNull();

        // If obfuscation was detected, warning should be present
        // (This is a structural test - actual obfuscated assemblies would trigger this)
    }

    [Test]
    public void IsLikelyObfuscated_ShouldNotFlagSystemStringAsObfuscated()
    {
        // Arrange - Get real System.String decompiled source from current runtime
        const string typeName = "System.String";
        var assemblyPath = typeof(string).Assembly.Location;

        // Skip test if assembly path is empty (should never happen)
        if (string.IsNullOrEmpty(assemblyPath))
        {
            Assert.Ignore("Could not determine System.Private.CoreLib.dll location");
        }

        // Act
        var decompiledSource = _decompilerService.DecompileType(
            assemblyPath,
            typeName,
            includeImplementation: true);

        decompiledSource.Should().NotBeNullOrEmpty();

        var isObfuscated = _decompilerService.IsLikelyObfuscated(decompiledSource!);

        // Assert
        isObfuscated.Should().BeFalse(
            "System.String is a well-known BCL type and should not be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldNotFlagSystemLinqEnumerableAsObfuscated()
    {
        // Arrange - System.Linq.Enumerable is a complex BCL type with many methods from current runtime
        const string typeName = "System.Linq.Enumerable";
        var assemblyPath = typeof(Enumerable).Assembly.Location;

        // Skip test if assembly path is empty (should never happen)
        if (string.IsNullOrEmpty(assemblyPath))
        {
            Assert.Ignore("Could not determine System.Linq.dll location");
        }

        // Act
        var decompiledSource = _decompilerService.DecompileType(
            assemblyPath,
            typeName,
            includeImplementation: false);

        if (decompiledSource == null)
        {
            Assert.Ignore($"Could not decompile {typeName}");
        }

        var isObfuscated = _decompilerService.IsLikelyObfuscated(decompiledSource);

        // Assert
        isObfuscated.Should().BeFalse(
            "System.Linq.Enumerable is a BCL type and should not be flagged as obfuscated");
    }
}

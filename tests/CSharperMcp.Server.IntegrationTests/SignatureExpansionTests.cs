using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests;

/// <summary>
/// Integration tests for signature expansion feature (USABILITY-REVIEW-v2.md lines 107-153).
/// Verifies that signatures are expanded to full declaration format.
/// </summary>
[TestFixture]
internal class SignatureExpansionTests
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
    public async Task GetSymbolInfo_ForClass_ReturnsFullDeclarationSignature()
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
        symbolInfo!.Name.Should().Be("Calculator");
        symbolInfo.Kind.Should().Be("NamedType");

        // New behavior: Should return "public class Calculator" instead of just "Calculator"
        symbolInfo.Signature.Should().NotBeNull();
        symbolInfo.Signature.Should().Contain("public");
        symbolInfo.Signature.Should().Contain("class");
        symbolInfo.Signature.Should().Contain("Calculator");
    }

    [Test]
    public async Task GetSymbolInfo_ForMethod_ReturnsFullDeclarationSignature()
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
        symbolInfo!.Name.Should().Be("Add");
        symbolInfo.Kind.Should().Be("Method");

        // New behavior: Should return "public int Add(int a, int b)" instead of "Add(int, int) -> int"
        symbolInfo.Signature.Should().NotBeNull();
        symbolInfo.Signature.Should().Contain("public");
        symbolInfo.Signature.Should().Contain("int");
        symbolInfo.Signature.Should().Contain("Add");
        symbolInfo.Signature.Should().Contain("(");
        symbolInfo.Signature.Should().Contain("a");
        symbolInfo.Signature.Should().Contain("b");
        symbolInfo.Signature.Should().Contain(")");
    }

    [Test]
    public async Task GetSymbolInfo_ForLocalVariable_ReturnsNullSignature()
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

        // Local variables should have null signature (per USABILITY-REVIEW-v2.md)
        symbolInfo.Signature.Should().BeNull();
    }

    [Test]
    public async Task GetSymbolInfo_ForBclClass_ReturnsFullDeclarationSignature()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for System.String
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "System.String",
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Name.Should().Be("String");
        symbolInfo.Kind.Should().Be("NamedType");

        // Should return full declaration with modifiers
        symbolInfo.Signature.Should().NotBeNull();
        symbolInfo.Signature.Should().Contain("public");
        symbolInfo.Signature.Should().Contain("class");
        symbolInfo.Signature.Should().Contain("String");
    }

    [Test]
    public async Task GetSymbolInfo_ForInterface_ReturnsFullDeclarationSignature()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol info for IDisposable
        var symbolInfo = await _sut.GetSymbolInfoAsync(
            symbolName: "System.IDisposable",
            includeDocumentation: false);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Name.Should().Be("IDisposable");
        symbolInfo.Kind.Should().Be("NamedType");

        // Should return "public interface IDisposable"
        symbolInfo.Signature.Should().NotBeNull();
        symbolInfo.Signature.Should().Contain("public");
        symbolInfo.Signature.Should().Contain("interface");
        symbolInfo.Signature.Should().Contain("IDisposable");
    }
}

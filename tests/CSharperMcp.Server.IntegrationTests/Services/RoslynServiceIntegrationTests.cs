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
        _decompilerService = new DecompilerService(_decompilerLoggerMock.Object);
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

    [Test]
    public async Task GetDiagnosticsAsync_WithNuGetSolution_CompilesCleanly()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - get errors (should be none)
        var diagnostics = (await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Error)).ToList();

        // Assert - SolutionWithNuGet should compile cleanly with NuGet references resolved
        diagnostics.Should().BeEmpty(because: "SolutionWithNuGet has no errors when NuGet packages are resolved");
    }

    [Test]
    public async Task CanResolveSymbolsFromNuGetPackage()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - get the compilation and verify we can find JObject type from Newtonsoft.Json
        var project = _workspaceManager.CurrentSolution!.Projects.First();
        var compilation = await project.GetCompilationAsync();

        // Assert
        compilation.Should().NotBeNull();

        // Verify we can resolve the JObject type from Newtonsoft.Json
        var jObjectType = compilation!.GetTypeByMetadataName("Newtonsoft.Json.Linq.JObject");
        jObjectType.Should().NotBeNull(because: "JObject type should be resolvable from NuGet package");

        // Verify it's from the correct assembly
        jObjectType!.ContainingAssembly.Name.Should().Be("Newtonsoft.Json");

        // Verify we can access type members
        var methods = jObjectType.GetMembers().OfType<Microsoft.CodeAnalysis.IMethodSymbol>().ToList();
        methods.Should().Contain(m => m.Name == "Parse", because: "JObject should have Parse method");
        methods.Should().Contain(m => m.Name == "FromObject", because: "JObject should have FromObject method");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ByName_ReturnsSystemStringInfo()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var symbolInfo = await _sut.GetSymbolInfoAsync(symbolName: "System.String");

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Name.Should().Be("String");
        symbolInfo.Kind.Should().Be("NamedType");
        symbolInfo.Namespace.Should().Be("System");
        symbolInfo.Assembly.Should().Contain("System");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ByName_ReturnsNuGetTypeInfo()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var symbolInfo = await _sut.GetSymbolInfoAsync(symbolName: "Newtonsoft.Json.Linq.JObject");

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Name.Should().Be("JObject");
        symbolInfo.Kind.Should().Be("NamedType");
        symbolInfo.Namespace.Should().Be("Newtonsoft.Json.Linq");
        symbolInfo.Assembly.Should().Be("Newtonsoft.Json");
    }

    [Test]
    public async Task GetSymbolInfoAsync_ByLocation_ReturnsMethodInfo()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get symbol at Calculator.cs line 5 (the Add method)
        var symbolInfo = await _sut.GetSymbolInfoAsync(filePath: "Calculator.cs", line: 5, column: 16);

        // Assert
        symbolInfo.Should().NotBeNull();
        symbolInfo!.Name.Should().Be("Add");
        symbolInfo.Kind.Should().Be("Method");
        symbolInfo.ContainingType.Should().Contain("Calculator");
        symbolInfo.Signature.Should().Contain("int");
    }

    [Test]
    public async Task GetSymbolInfoAsync_WithInvalidLocation_ReturnsNull()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var symbolInfo = await _sut.GetSymbolInfoAsync(filePath: "NonExistent.cs", line: 1, column: 1);

        // Assert
        symbolInfo.Should().BeNull();
    }

    [Test]
    public async Task FindReferencesAsync_ByLocation_FindsAllUsages()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Find references to Calculator.Add method (defined at Calculator.cs line 5)
        var references = (await _sut.FindReferencesAsync(filePath: "Calculator.cs", line: 5, column: 16)).ToList();

        // Assert - Should find the definition + 3 usages in Program.cs
        references.Should().NotBeEmpty();
        references.Should().Contain(r => r.FilePath.EndsWith("Program.cs") && r.ContextSnippet.Contains("Add"));

        // Should have at least 3 references in Program.cs (lines 9, 10, 11)
        var programReferences = references.Where(r => r.FilePath.EndsWith("Program.cs")).ToList();
        programReferences.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Test]
    public async Task FindReferencesAsync_ByName_FindsTypeUsages()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Find references to Calculator type
        var references = (await _sut.FindReferencesAsync(symbolName: "SimpleProject.Calculator")).ToList();

        // Assert - Should find instantiation in Program.cs
        references.Should().NotBeEmpty();
        references.Should().Contain(r =>
            r.FilePath.EndsWith("Program.cs") &&
            r.ContextSnippet.Contains("Calculator"));
    }

    [Test]
    public async Task FindReferencesAsync_WithInvalidSymbol_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var references = (await _sut.FindReferencesAsync(filePath: "NonExistent.cs", line: 1, column: 1)).ToList();

        // Assert
        references.Should().BeEmpty();
    }

    [Test]
    public async Task FindReferencesAsync_ReturnsLocationDetails()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var references = (await _sut.FindReferencesAsync(filePath: "Calculator.cs", line: 5, column: 16)).ToList();

        // Assert
        references.Should().NotBeEmpty();
        references.Should().AllSatisfy(r =>
        {
            r.FilePath.Should().NotBeNullOrEmpty();
            r.Line.Should().BeGreaterThan(0);
            r.Column.Should().BeGreaterThan(0);
            r.ContextSnippet.Should().NotBeNullOrEmpty();
            r.ReferenceKind.Should().BeOneOf("Explicit", "Implicit");
        });
    }

    [Test]
    public async Task GetDefinitionAsync_ForWorkspaceSymbol_ReturnsSourceLocation()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition at Calculator.Add method usage (Program.cs line 10)
        var definition = await _sut.GetDefinitionAsync(filePath: "Program.cs", line: 10, column: 27);

        // Assert - Should return source location of Add method in Calculator.cs
        definition.Should().NotBeNull();
        definition!.IsSourceLocation.Should().BeTrue();
        definition.FilePath.Should().EndWith("Calculator.cs");
        definition.Line.Should().Be(5); // Add method is on line 5 in Calculator.cs
        definition.Column.Should().BeGreaterThan(0);
        definition.Assembly.Should().Be("SimpleProject");
    }

    [Test]
    public async Task GetDefinitionAsync_ForBclType_ReturnsDecompiledSource()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for System.Console by name (used in Program.cs)
        var definition = await _sut.GetDefinitionAsync(symbolName: "System.Console");

        // Assert - Should return decompiled source from BCL
        definition.Should().NotBeNull();
        definition!.IsSourceLocation.Should().BeFalse();
        definition.DecompiledSource.Should().NotBeNullOrEmpty();
        definition.DecompiledSource.Should().Contain("class Console");
        definition.Assembly.Should().Contain("System");
    }

    [Test]
    public async Task GetDefinitionAsync_ForNuGetType_ReturnsDecompiledSource()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for JObject from Newtonsoft.Json
        var definition = await _sut.GetDefinitionAsync(symbolName: "Newtonsoft.Json.Linq.JObject");

        // Assert - Should return decompiled source from NuGet package
        definition.Should().NotBeNull();
        definition!.IsSourceLocation.Should().BeFalse();
        definition.DecompiledSource.Should().NotBeNullOrEmpty();
        definition.DecompiledSource.Should().Contain("JObject");
        definition.Assembly.Should().Be("Newtonsoft.Json");
        definition.Package.Should().Be("Newtonsoft.Json");
    }

    [Test]
    public async Task GetDefinitionAsync_WithInvalidSymbol_ReturnsNull()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var definition = await _sut.GetDefinitionAsync(filePath: "NonExistent.cs", line: 1, column: 1);

        // Assert
        definition.Should().BeNull();
    }

    [Test]
    public async Task GetDefinitionAsync_ByLocation_ForUserType_ReturnsSourceLocation()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for Calculator type at instantiation (Program.cs line 7)
        var definition = await _sut.GetDefinitionAsync(filePath: "Program.cs", line: 7, column: 24);

        // Assert - Should return source location of Calculator class
        definition.Should().NotBeNull();
        definition!.IsSourceLocation.Should().BeTrue();
        definition.FilePath.Should().EndWith("Calculator.cs");
        definition.Line.Should().Be(3); // Calculator class starts at line 3
        definition.Assembly.Should().Be("SimpleProject");
    }

    [Test]
    public async Task GetTypeMembersAsync_ForWorkspaceType_ReturnsFullSourceCode()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get full definition of Calculator class
        var typeMembers = await _sut.GetTypeMembersAsync("SimpleProject.Calculator");

        // Assert - Should return complete source code with all methods
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeTrue();
        typeMembers.FilePath.Should().EndWith("Calculator.cs");
        typeMembers.TypeName.Should().Be("Calculator");
        typeMembers.Namespace.Should().Be("SimpleProject");
        typeMembers.Assembly.Should().Be("SimpleProject");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.SourceCode.Should().Contain("public class Calculator");
        typeMembers.SourceCode.Should().Contain("public int Add");
        typeMembers.SourceCode.Should().Contain("public int Subtract");
        typeMembers.SourceCode.Should().Contain("public int Multiply");
        typeMembers.SourceCode.Should().Contain("public double Divide");
    }

    [Test]
    public async Task GetTypeMembersAsync_ForBclType_ReturnsDecompiledSource()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get full definition of System.Console
        var typeMembers = await _sut.GetTypeMembersAsync("System.Console");

        // Assert - Should return decompiled source with members
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeFalse();
        typeMembers.FilePath.Should().BeNull();
        typeMembers.TypeName.Should().Be("Console");
        typeMembers.Namespace.Should().Be("System");
        typeMembers.Assembly.Should().Contain("System");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.SourceCode.Should().Contain("class Console");
        typeMembers.SourceCode.Should().Contain("WriteLine");
    }

    [Test]
    public async Task GetTypeMembersAsync_ForNuGetType_ReturnsDecompiledSource()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get full definition of JObject from Newtonsoft.Json
        var typeMembers = await _sut.GetTypeMembersAsync("Newtonsoft.Json.Linq.JObject");

        // Assert - Should return decompiled source with all members
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeFalse();
        typeMembers.FilePath.Should().BeNull();
        typeMembers.TypeName.Should().Be("JObject");
        typeMembers.Namespace.Should().Be("Newtonsoft.Json.Linq");
        typeMembers.Assembly.Should().Be("Newtonsoft.Json");
        typeMembers.Package.Should().Be("Newtonsoft.Json");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.SourceCode.Should().Contain("JObject");
        typeMembers.SourceCode.Should().Contain("Parse");
        typeMembers.SourceCode.Should().Contain("FromObject");
    }

    [Test]
    public async Task GetTypeMembersAsync_WithInvalidType_ReturnsNull()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var typeMembers = await _sut.GetTypeMembersAsync("NonExistent.Type");

        // Assert
        typeMembers.Should().BeNull();
    }
}

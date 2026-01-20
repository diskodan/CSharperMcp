using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.IntegrationTests.Services;

internal class RoslynServiceIntegrationTests
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
    public async Task GetDiagnosticsAsync_WithSolutionWithErrors_ReturnsExpectedErrors()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var (diagnostics, _) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Warning);
        var diagnosticsList = diagnostics.ToList();

        // Assert - we expect specific errors from our test fixture:
        // CS0103 (line 8): undeclared variable
        // CS0029 (line 14): type conversion error
        // CS1061 (line 21): missing method
        // CS0414 (line 6): unused field warning
        diagnosticsList.Should().NotBeEmpty();

        var errorCodes = diagnosticsList.Select(d => d.Id).Distinct().ToList();
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
        var (diagnostics, _) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Error);
        var diagnosticsList = diagnostics.ToList();

        // Assert - SimpleSolution should compile cleanly
        diagnosticsList.Should().BeEmpty(because: "SimpleSolution has no errors");
    }

    [Test]
    public async Task GetDiagnosticsAsync_FilterByFile_ReturnsOnlyMatchingDiagnostics()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - filter to only ClassWithErrors.cs
        var (diagnostics, _) = await _sut.GetDiagnosticsAsync(
            filePath: "ClassWithErrors.cs",
            minimumSeverity: DiagnosticSeverity.Warning);
        var diagnosticsList = diagnostics.ToList();

        // Assert - should only get diagnostics from ClassWithErrors.cs
        diagnosticsList.Should().NotBeEmpty();
        diagnosticsList.Should().AllSatisfy(d =>
            d.Location.SourceTree?.FilePath.Should().EndWith("ClassWithErrors.cs"));
    }

    [Test]
    public async Task GetDiagnosticsAsync_FilterByLineRange_ReturnsOnlyMatchingDiagnostics()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - filter to lines 1-10 (should include CS0103 on line 8)
        var (diagnostics, _) = await _sut.GetDiagnosticsAsync(
            startLine: 1,
            endLine: 10,
            minimumSeverity: DiagnosticSeverity.Error);
        var diagnosticsList = diagnostics.ToList();

        // Assert - should find CS0103 error around line 8
        diagnosticsList.Should().NotBeEmpty();
        diagnosticsList.Should().Contain(d => d.Id == "CS0103");
    }

    [Test]
    public async Task GetDiagnosticsAsync_FilterBySeverity_ReturnsOnlyMatchingSeverity()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - get errors only (should exclude CS0414 warning)
        var (errorDiagnostics, _) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Error);
        var errorDiagnosticsList = errorDiagnostics.ToList();

        // Assert
        errorDiagnosticsList.Should().NotBeEmpty();
        errorDiagnosticsList.Should().AllSatisfy(d =>
            d.Severity.Should().Be(DiagnosticSeverity.Error));
        errorDiagnosticsList.Should().NotContain(d => d.Id == "CS0414",
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
        var (diagnostics, _) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Error);
        var diagnosticsList = diagnostics.ToList();

        // Assert - SolutionWithNuGet should compile cleanly with NuGet references resolved
        diagnosticsList.Should().BeEmpty(because: "SolutionWithNuGet has no errors when NuGet packages are resolved");
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
    public async Task FindSymbolUsagesAsync_ByLocation_FindsAllUsages()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Find references to Calculator.Add method (defined at Calculator.cs line 5)
        var result = await _sut.FindSymbolUsagesAsync(filePath: "Calculator.cs", line: 5, column: 16);
        var references = result.Usages.ToList();

        // Assert - Should find the definition + 3 usages in Program.cs
        references.Should().NotBeEmpty();
        references.Should().Contain(r => r.FilePath.EndsWith("Program.cs") && r.ContextSnippet.Contains("Add"));

        // Should have at least 3 references in Program.cs (lines 9, 10, 11)
        var programReferences = references.Where(r => r.FilePath.EndsWith("Program.cs")).ToList();
        programReferences.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Test]
    public async Task FindSymbolUsagesAsync_ByName_FindsTypeUsages()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Find references to Calculator type
        var result = await _sut.FindSymbolUsagesAsync(symbolName: "SimpleProject.Calculator");
        var references = result.Usages;

        // Assert - Should find instantiation in Program.cs
        references.Should().NotBeEmpty();
        references.Should().Contain(r =>
            r.FilePath.EndsWith("Program.cs") &&
            r.ContextSnippet.Contains("Calculator"));
    }

    [Test]
    public async Task FindSymbolUsagesAsync_WithInvalidSymbol_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.FindSymbolUsagesAsync(filePath: "NonExistent.cs", line: 1, column: 1);
        var references = result.Usages;

        // Assert
        references.Should().BeEmpty();
    }

    [Test]
    public async Task FindSymbolUsagesAsync_ReturnsLocationDetails()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.FindSymbolUsagesAsync(filePath: "Calculator.cs", line: 5, column: 16);
        var references = result.Usages.ToList();

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
        definition!.IsFromWorkspace.Should().BeTrue();
        definition.FilePath.Should().EndWith("Calculator.cs");
        definition.Line.Should().Be(5); // Add method is on line 5 in Calculator.cs
        definition.Column.Should().BeGreaterThan(0);
        definition.Assembly.Should().Be("SimpleProject");
    }

    [Test]
    public async Task GetDefinitionAsync_ForBclType_ReturnsMetadata()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for System.Console by name (used in Program.cs)
        var definition = await _sut.GetDefinitionAsync(symbolName: "System.Console");

        // Assert - Should return metadata from BCL (no decompiled source)
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();
        definition.Assembly.Should().Contain("System");
        definition.TypeName.Should().Contain("Console");
        definition.SymbolKind.Should().NotBeNullOrEmpty();
        definition.Signature.Should().NotBeNullOrEmpty();

        // Verify no decompiled source is present
        definition.FilePath.Should().BeNull();
        definition.Line.Should().BeNull();
        definition.Column.Should().BeNull();
    }

    [Test]
    public async Task GetDefinitionAsync_ForNuGetType_ReturnsMetadata()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithNuGet"), "SolutionWithNuGet.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get definition for JObject from Newtonsoft.Json
        var definition = await _sut.GetDefinitionAsync(symbolName: "Newtonsoft.Json.Linq.JObject");

        // Assert - Should return metadata from NuGet package (no decompiled source)
        definition.Should().NotBeNull();
        definition!.IsFromWorkspace.Should().BeFalse();
        definition.Assembly.Should().Be("Newtonsoft.Json");
        definition.TypeName.Should().Contain("JObject");
        definition.SymbolKind.Should().NotBeNullOrEmpty();
        definition.Signature.Should().NotBeNullOrEmpty();
        definition.Package.Should().Be("Newtonsoft.Json");

        // Verify no decompiled source is present
        definition.FilePath.Should().BeNull();
        definition.Line.Should().BeNull();
        definition.Column.Should().BeNull();
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
        definition!.IsFromWorkspace.Should().BeTrue();
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

    [Test]
    public async Task GetTypeMembersAsync_WithIncludeImplementationFalse_ReturnsSignaturesOnly()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get signatures only
        var typeMembers = await _sut.GetTypeMembersAsync("SimpleProject.Calculator", includeInherited: false, includeImplementation: false);

        // Assert - Should return signatures without method bodies
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeTrue();
        typeMembers.TypeName.Should().Be("Calculator");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.IncludesImplementation.Should().BeFalse();
        typeMembers.LineCount.Should().BeGreaterThan(0);

        // Source code should contain method signatures
        typeMembers.SourceCode.Should().Contain("int Add");
        typeMembers.SourceCode.Should().Contain("int Subtract");
        typeMembers.SourceCode.Should().Contain("int Multiply");
        typeMembers.SourceCode.Should().Contain("double Divide");

        // Should contain method signatures ending with semicolons
        typeMembers.SourceCode.Should().Contain(";");

        // Should not contain implementation details
        typeMembers.SourceCode.Should().NotContain("=> a + b");
        typeMembers.SourceCode.Should().NotContain("=> a - b");
        typeMembers.SourceCode.Should().NotContain("throw new DivideByZeroException");
    }

    [Test]
    public async Task GetTypeMembersAsync_WithIncludeImplementationTrue_ReturnsFullSource()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get full source
        var typeMembers = await _sut.GetTypeMembersAsync("SimpleProject.Calculator", includeInherited: false, includeImplementation: true);

        // Assert - Should return full source with method bodies
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeTrue();
        typeMembers.TypeName.Should().Be("Calculator");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.IncludesImplementation.Should().BeTrue();
        typeMembers.LineCount.Should().BeGreaterThan(0);

        // Source code should contain full method implementations (expression-bodied or block-bodied)
        // The Calculator uses expression-bodied methods (=>) and one block method (Divide)
        typeMembers.SourceCode.Should().Contain("=> a + b");
        typeMembers.SourceCode.Should().Contain("=> a - b");
        typeMembers.SourceCode.Should().Contain("=> a * b");
        typeMembers.SourceCode.Should().Contain("throw new DivideByZeroException");
    }

    [Test]
    public async Task GetTypeMembersAsync_ForBclType_WithIncludeImplementationFalse_ReturnsSignaturesOnly()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get System.Console signatures only
        var typeMembers = await _sut.GetTypeMembersAsync("System.Console", includeInherited: false, includeImplementation: false);

        // Assert - Should return decompiled signatures without method bodies
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeFalse();
        typeMembers.TypeName.Should().Be("Console");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.IncludesImplementation.Should().BeFalse();
        typeMembers.LineCount.Should().BeGreaterThan(0);

        // Should contain method signatures
        typeMembers.SourceCode.Should().Contain("WriteLine");

        // Verify the parameter is being passed correctly
        var fullTypeMembers = await _sut.GetTypeMembersAsync("System.Console", includeInherited: false, includeImplementation: true);
        fullTypeMembers.Should().NotBeNull();
        fullTypeMembers!.IncludesImplementation.Should().BeTrue();

        // The signatures-only version should be different from the full version
        // (though the line count may not be dramatically different due to decompiler settings)
        typeMembers.IncludesImplementation.Should().NotBe(fullTypeMembers.IncludesImplementation);
    }

    [Test]
    public async Task GetTypeMembersAsync_ForBclType_WithIncludeImplementationTrue_ReturnsFullSource()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - Get System.Console full source
        var typeMembers = await _sut.GetTypeMembersAsync("System.Console", includeInherited: false, includeImplementation: true);

        // Assert - Should return decompiled source with method bodies
        typeMembers.Should().NotBeNull();
        typeMembers!.IsFromWorkspace.Should().BeFalse();
        typeMembers.TypeName.Should().Be("Console");
        typeMembers.SourceCode.Should().NotBeNullOrEmpty();
        typeMembers.IncludesImplementation.Should().BeTrue();
        typeMembers.LineCount.Should().BeGreaterThan(0);

        // Full implementation should contain actual code
        typeMembers.SourceCode.Should().Contain("WriteLine");
    }

    [Test]
    public async Task GetTypeMembersAsync_LineCount_IsAccurate()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var typeMembers = await _sut.GetTypeMembersAsync("SimpleProject.Calculator");

        // Assert
        typeMembers.Should().NotBeNull();
        var actualLineCount = typeMembers!.SourceCode.Split('\n').Length;
        typeMembers.LineCount.Should().Be(actualLineCount, because: "LineCount should match actual line count");
    }

    [Test]
    public async Task GetDiagnosticsAsync_WithPagination_ReturnsCorrectSubset()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all diagnostics first to know the total count
        var (allDiagnostics, totalCount) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Warning);
        var allDiagnosticsList = allDiagnostics.ToList();

        // Act - get first 2 diagnostics
        var (firstPage, firstPageTotal) = await _sut.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 2,
            offset: 0);
        var firstPageList = firstPage.ToList();

        // Assert
        firstPageTotal.Should().Be(totalCount);
        firstPageList.Should().HaveCount(2);
        firstPageList[0].Id.Should().Be(allDiagnosticsList[0].Id);
        firstPageList[1].Id.Should().Be(allDiagnosticsList[1].Id);
    }

    [Test]
    public async Task GetDiagnosticsAsync_WithOffset_ReturnsCorrectPage()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all diagnostics first
        var (allDiagnostics, totalCount) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Warning);
        var allDiagnosticsList = allDiagnostics.ToList();

        // Act - get second page (skip 2, take 2)
        var (secondPage, secondPageTotal) = await _sut.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 2,
            offset: 2);
        var secondPageList = secondPage.ToList();

        // Assert
        secondPageTotal.Should().Be(totalCount);
        if (totalCount > 2)
        {
            secondPageList.Should().HaveCountLessOrEqualTo(2);
            secondPageList[0].Id.Should().Be(allDiagnosticsList[2].Id);
        }
    }

    [Test]
    public async Task GetDiagnosticsAsync_WithOffsetBeyondTotal_ReturnsEmpty()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get total count
        var (_, totalCount) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Warning);

        // Act - request beyond total
        var (diagnostics, returnedTotal) = await _sut.GetDiagnosticsAsync(
            minimumSeverity: DiagnosticSeverity.Warning,
            maxResults: 10,
            offset: totalCount + 100);
        var diagnosticsList = diagnostics.ToList();

        // Assert
        returnedTotal.Should().Be(totalCount);
        diagnosticsList.Should().BeEmpty();
    }

    [Test]
    public async Task GetDiagnosticsAsync_DefaultPagination_Returns100OrLess()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SolutionWithErrors"), "SolutionWithErrors.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act - use default pagination (maxResults = 100)
        var (diagnostics, totalCount) = await _sut.GetDiagnosticsAsync(minimumSeverity: DiagnosticSeverity.Warning);
        var diagnosticsList = diagnostics.ToList();

        // Assert - should return at most 100 results
        diagnosticsList.Should().HaveCountLessOrEqualTo(100);
        totalCount.Should().BeGreaterOrEqualTo(diagnosticsList.Count);
    }
    [Test]
    public async Task FindSymbolUsagesAsync_WithPagination_ReturnsCorrectSubset()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all references first
        var allResult = await _sut.FindSymbolUsagesAsync(filePath: "Calculator.cs", line: 5, column: 16);
        var totalCount = allResult.TotalCount;

        // Act - Get first 2 references
        var firstPageResult = await _sut.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            maxResults: 2,
            offset: 0);

        // Assert
        firstPageResult.TotalCount.Should().Be(totalCount);
        firstPageResult.Usages.Should().HaveCountLessOrEqualTo(2);
        firstPageResult.HasMore.Should().Be(totalCount > 2);

        if (totalCount > 2)
        {
            firstPageResult.Usages.Count.Should().Be(2);
        }
    }

    [Test]
    public async Task FindSymbolUsagesAsync_WithOffset_SkipsCorrectNumberOfResults()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all references
        var allResult = await _sut.FindSymbolUsagesAsync(filePath: "Calculator.cs", line: 5, column: 16);
        var allReferences = allResult.Usages.ToList();
        var totalCount = allResult.TotalCount;

        if (totalCount < 3)
        {
            Assert.Inconclusive("Need at least 3 references for this test");
            return;
        }

        // Act - Skip first 2 references
        var offsetResult = await _sut.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            maxResults: 100,
            offset: 2);

        // Assert
        offsetResult.TotalCount.Should().Be(totalCount);
        offsetResult.Usages.Should().HaveCount(totalCount - 2);
        
        // First reference in offset result should match third reference from all results
        if (offsetResult.Usages.Count > 0)
        {
            offsetResult.Usages[0].FilePath.Should().Be(allReferences[2].FilePath);
            offsetResult.Usages[0].Line.Should().Be(allReferences[2].Line);
            offsetResult.Usages[0].Column.Should().Be(allReferences[2].Column);
        }
    }

    [Test]
    public async Task FindSymbolUsagesAsync_WithContextLinesOne_ReturnsSingleLine()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.FindSymbolUsagesAsync(
            filePath: "Calculator.cs",
            line: 5,
            column: 16,
            contextLines: 1);

        // Assert
        result.Usages.Should().NotBeEmpty();
        foreach (var reference in result.Usages)
        {
            // Context should be a single line (no newlines)
            reference.ContextSnippet.Should().NotContain(Environment.NewLine);
        }
    }

    [Test]
    public async Task FindSymbolUsagesAsync_WithContextLinesThree_ReturnsThreeLines()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.FindSymbolUsagesAsync(
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
            // Context should have 3 lines (or fewer if near file boundaries)
            var lines = middleReference.ContextSnippet.Split(Environment.NewLine);
            lines.Should().HaveCountGreaterOrEqualTo(2); // At least 2 lines
            lines.Should().HaveCountLessOrEqualTo(3); // At most 3 lines
        }
    }

    [Test]
    public async Task FindSymbolUsagesAsync_WithContextLinesFive_ReturnsFiveLines()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Act
        var result = await _sut.FindSymbolUsagesAsync(
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
            lines.Should().HaveCountGreaterOrEqualTo(3); // At least 3 lines
            lines.Should().HaveCountLessOrEqualTo(5); // At most 5 lines
        }
    }

    [Test]
    public async Task FindSymbolUsagesAsync_PaginationMetadata_IsAccurate()
    {
        // Arrange
        var solutionPath = Path.Combine(GetFixturePath("SimpleSolution"), "SimpleSolution.sln");
        await _workspaceManager.InitializeAsync(solutionPath);

        // Get all references
        var allResult = await _sut.FindSymbolUsagesAsync(filePath: "Calculator.cs", line: 5, column: 16);
        var totalCount = allResult.TotalCount;

        if (totalCount < 2)
        {
            Assert.Inconclusive("Need at least 2 references for this test");
            return;
        }

        // Act - Get first page with maxResults = 1
        var firstPage = await _sut.FindSymbolUsagesAsync(
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
        var lastPage = await _sut.FindSymbolUsagesAsync(
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
}

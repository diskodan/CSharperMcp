using FluentAssertions;
using CSharperMcp.Server.Models;

namespace CSharperMcp.Server.UnitTests.Services;

[TestFixture]
public class CodeActionsServiceTests
{
    [Test]
    public void ApplyCodeActionResult_WithSuccess_HasExpectedProperties()
    {
        // Arrange & Act
        var result = new ApplyCodeActionResult(
            Success: true,
            ErrorMessage: null,
            Changes: new List<FileChange>()
        );

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Changes.Should().NotBeNull();
    }

    [Test]
    public void ApplyCodeActionResult_WithFailure_HasExpectedProperties()
    {
        // Arrange & Act
        var result = new ApplyCodeActionResult(
            Success: false,
            ErrorMessage: "Test error",
            Changes: Array.Empty<FileChange>()
        );

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test error");
        result.Changes.Should().BeEmpty();
    }

    [Test]
    public void FileChange_WithModification_HasExpectedProperties()
    {
        // Arrange & Act
        var change = new FileChange(
            FilePath: "/path/to/file.cs",
            OriginalContent: "original",
            ModifiedContent: "modified",
            UnifiedDiff: "diff output"
        );

        // Assert
        change.FilePath.Should().Be("/path/to/file.cs");
        change.OriginalContent.Should().Be("original");
        change.ModifiedContent.Should().Be("modified");
        change.UnifiedDiff.Should().Be("diff output");
    }

    [Test]
    public void FileChange_WithAddition_HasNullOriginalContent()
    {
        // Arrange & Act
        var change = new FileChange(
            FilePath: "/path/to/new/file.cs",
            OriginalContent: null,
            ModifiedContent: "new content",
            UnifiedDiff: "+new content"
        );

        // Assert
        change.FilePath.Should().Be("/path/to/new/file.cs");
        change.OriginalContent.Should().BeNull();
        change.ModifiedContent.Should().Be("new content");
    }

    [Test]
    public void FileChange_WithDeletion_HasNullModifiedContent()
    {
        // Arrange & Act
        var change = new FileChange(
            FilePath: "/path/to/deleted/file.cs",
            OriginalContent: "old content",
            ModifiedContent: null,
            UnifiedDiff: "-old content"
        );

        // Assert
        change.FilePath.Should().Be("/path/to/deleted/file.cs");
        change.OriginalContent.Should().Be("old content");
        change.ModifiedContent.Should().BeNull();
    }
}

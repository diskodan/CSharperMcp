using FluentAssertions;
using CSharperMcp.Server.Models;

namespace CSharperMcp.Server.UnitTests.Services;

[TestFixture]
public class CodeActionsServiceTests
{
    [Test]
    public void CodeActionsResult_WithFullPage_HasCorrectProperties()
    {
        // Arrange
        var actions = new List<CodeActionInfo>
        {
            new CodeActionInfo("id1", "Action 1", "QuickFix", new[] { "CS0001" }),
            new CodeActionInfo("id2", "Action 2", "QuickFix", new[] { "CS0002" }),
            new CodeActionInfo("id3", "Action 3", "Refactor", Array.Empty<string>())
        };

        // Act
        var result = new CodeActionsResult(
            Actions: actions,
            TotalCount: 10,
            ReturnedCount: 3,
            HasMore: true
        );

        // Assert
        result.Actions.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
        result.ReturnedCount.Should().Be(3);
        result.HasMore.Should().BeTrue();
    }

    [Test]
    public void CodeActionsResult_WithLastPage_HasMoreIsFalse()
    {
        // Arrange & Act
        var result = new CodeActionsResult(
            Actions: new List<CodeActionInfo>
            {
                new CodeActionInfo("id1", "Action 1", "QuickFix", new[] { "CS0001" })
            },
            TotalCount: 1,
            ReturnedCount: 1,
            HasMore: false
        );

        // Assert
        result.HasMore.Should().BeFalse();
        result.TotalCount.Should().Be(result.ReturnedCount);
    }

    [Test]
    public void CodeActionsResult_WithEmptyResults_HasCorrectProperties()
    {
        // Arrange & Act
        var result = new CodeActionsResult(
            Actions: Array.Empty<CodeActionInfo>(),
            TotalCount: 0,
            ReturnedCount: 0,
            HasMore: false
        );

        // Assert
        result.Actions.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.ReturnedCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }


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

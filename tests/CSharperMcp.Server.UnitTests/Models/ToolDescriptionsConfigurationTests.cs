using System.Text;
using CSharperMcp.Server.Models;
using Microsoft.Extensions.Configuration;

namespace CSharperMcp.Server.UnitTests.Models;

[TestFixture]
internal class ToolDescriptionsConfigurationTests
{
    [Test]
    public void ConfigurationBinding_WithValidYaml_BindsCorrectly()
    {
        // Arrange
        var yaml =
            @"
tools:
  initialize_workspace:
    description: Custom workspace description
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools.Should().ContainKey("initialize_workspace");
        toolConfig
            .Tools["initialize_workspace"]
            .Description.Should()
            .Be("Custom workspace description");
    }

    [Test]
    public void ConfigurationBinding_WithMultipleTools_BindsAllTools()
    {
        // Arrange
        var yaml =
            @"
tools:
  tool1:
    description: First tool description
  tool2:
    description: Second tool description
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools.Should().HaveCount(2);
        toolConfig.Tools["tool1"].Description.Should().Be("First tool description");
        toolConfig.Tools["tool2"].Description.Should().Be("Second tool description");
    }

    [Test]
    public void ConfigurationMerging_SecondSourceOverridesFirst()
    {
        // Arrange
        var yaml1 =
            @"
tools:
  test_tool:
    description: Original description
";
        var yaml2 =
            @"
tools:
  test_tool:
    description: Overridden description
";
        var configBuilder = new ConfigurationBuilder()
            .AddYamlStream(new MemoryStream(Encoding.UTF8.GetBytes(yaml1)))
            .AddYamlStream(new MemoryStream(Encoding.UTF8.GetBytes(yaml2)));
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["test_tool"].Description.Should().Be("Overridden description");
    }

    [Test]
    public void ConfigurationBinding_WithEmptyYaml_CreatesEmptyConfiguration()
    {
        // Arrange
        var yaml =
            @"
tools: {}
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools.Should().BeEmpty();
    }

    [Test]
    public void ConfigurationBinding_WithOnlyDescription_BindsCorrectly()
    {
        // Arrange
        var yaml =
            @"
tools:
  simple_tool:
    description: Simple tool with no params
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["simple_tool"].Description.Should().Be("Simple tool with no params");
    }

    [Test]
    public void ConfigurationBinding_WithNullDescription_AllowsNullDescription()
    {
        // Arrange
        var yaml =
            @"
tools:
  test_tool:
    isEnabled: true
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["test_tool"].Description.Should().BeNull();
    }

    [Test]
    public void ConfigurationMerging_PreservesUnmodifiedValues()
    {
        // Arrange
        var yaml1 =
            @"
tools:
  tool1:
    description: Tool 1 description
  tool2:
    description: Tool 2 description
";
        var yaml2 =
            @"
tools:
  tool2:
    description: Tool 2 overridden
";
        var configBuilder = new ConfigurationBuilder()
            .AddYamlStream(new MemoryStream(Encoding.UTF8.GetBytes(yaml1)))
            .AddYamlStream(new MemoryStream(Encoding.UTF8.GetBytes(yaml2)));
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["tool1"].Description.Should().Be("Tool 1 description");
        toolConfig.Tools["tool2"].Description.Should().Be("Tool 2 overridden");
    }

    [Test]
    public void ConfigurationBinding_WithIsEnabledTrue_BindsCorrectly()
    {
        // Arrange
        var yaml =
            @"
tools:
  test_tool:
    description: Test description
    isEnabled: true
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["test_tool"].IsEnabled.Should().BeTrue();
    }

    [Test]
    public void ConfigurationBinding_WithIsEnabledFalse_BindsCorrectly()
    {
        // Arrange
        var yaml =
            @"
tools:
  test_tool:
    description: Test description
    isEnabled: false
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["test_tool"].IsEnabled.Should().BeFalse();
    }

    [Test]
    public void ConfigurationBinding_WithoutIsEnabled_DefaultsToTrue()
    {
        // Arrange
        var yaml =
            @"
tools:
  test_tool:
    description: Test description
";
        var configBuilder = new ConfigurationBuilder().AddYamlStream(
            new MemoryStream(Encoding.UTF8.GetBytes(yaml))
        );
        var config = configBuilder.Build();

        // Act
        var toolConfig = new ToolDescriptionsConfiguration();
        config.GetSection("tools").Bind(toolConfig.Tools);

        // Assert
        toolConfig.Tools["test_tool"].IsEnabled.Should().BeTrue();
    }
}

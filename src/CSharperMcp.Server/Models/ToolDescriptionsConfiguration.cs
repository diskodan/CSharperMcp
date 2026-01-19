namespace CSharperMcp.Server.Models;

/// <summary>
/// Configuration for customizing MCP tool descriptions.
/// Default values are defined as property initializers and can be overridden via YAML configuration files.
/// </summary>
internal class ToolDescriptionsConfiguration
{
    /// <summary>
    /// Tool-specific configuration overrides, keyed by tool name.
    /// </summary>
    public Dictionary<string, ToolDescriptionOverride> Tools { get; set; } = new();
}

/// <summary>
/// Configuration for a specific tool's description and visibility.
/// </summary>
internal class ToolDescriptionOverride
{
    /// <summary>
    /// Override for the tool's description.
    /// If null or not specified, uses the default from [Description] attribute.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this tool should be visible in the tools list.
    /// If false, the tool will be filtered out from ListTools response.
    /// Defaults to true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

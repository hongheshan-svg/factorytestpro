using System.Collections.Generic;
using System.Linq;
using UTF.Plugin.Abstractions;
using UTF.UI.Services;
using Xunit;

namespace UTF.UI.Tests;

/// <summary>
/// Unit tests for <see cref="PluginCapabilityOptions"/> merge / fallback behavior.
/// </summary>
public class PluginCapabilityOptionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void MergeStepTypes_NullPlugins_ReturnsFallbacks()
    {
        var result = PluginCapabilityOptions.MergeStepTypes(null);

        Assert.Equal(
            PluginCapabilityOptions.FallbackStepTypes.OrderBy(s => s, System.StringComparer.OrdinalIgnoreCase),
            result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MergeChannels_EmptyPlugins_ReturnsFallbacks()
    {
        var result = PluginCapabilityOptions.MergeChannels(System.Array.Empty<PluginMetadata>());

        Assert.Equal(
            PluginCapabilityOptions.FallbackChannels.OrderBy(s => s, System.StringComparer.OrdinalIgnoreCase),
            result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MergeStepTypes_WithDuplicates_IsCaseInsensitiveDistinctAndSorted()
    {
        var plugins = new List<PluginMetadata>
        {
            new()
            {
                PluginId = "a",
                SupportedStepTypes = new[] { "Serial", "custom", "  " },
                SupportedChannels = new[] { "COM" }
            },
            new()
            {
                PluginId = "b",
                SupportedStepTypes = new[] { "serial", "adb" },
                SupportedChannels = new[] { "serial" }
            }
        };

        var types = PluginCapabilityOptions.MergeStepTypes(plugins);
        var channels = PluginCapabilityOptions.MergeChannels(plugins);

        Assert.Equal(3, types.Count);
        Assert.Equal(new[] { "adb", "custom", "serial" }, types.Select(t => t.ToLowerInvariant()).ToArray());
        Assert.Equal(2, channels.Count);
        Assert.Contains(channels, c => c.Equals("com", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(channels, c => c.Equals("serial", System.StringComparison.OrdinalIgnoreCase));
    }
}

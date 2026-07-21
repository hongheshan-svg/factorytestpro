using System.Collections.Generic;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using Xunit;

namespace UTF.Plugin.Host.Tests;

/// <summary>
/// Unit tests for <see cref="PluginCapabilityService"/> matching and aggregation.
/// </summary>
public class PluginCapabilityServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Matches_WildcardInSupported_AlwaysTrue()
    {
        Assert.True(PluginCapabilityService.Matches(new[] { "*" }, "serial"));
        Assert.True(PluginCapabilityService.Matches(new[] { "*" }, null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Matches_ConcreteList_IsCaseInsensitive()
    {
        var supported = new[] { "serial", "uart" };
        Assert.True(PluginCapabilityService.Matches(supported, "Serial"));
        Assert.True(PluginCapabilityService.Matches(supported, "UART"));
        Assert.False(PluginCapabilityService.Matches(supported, "telnet"));
        Assert.False(PluginCapabilityService.Matches(supported, null));
        Assert.False(PluginCapabilityService.Matches(supported, ""));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetAllStepTypes_AndChannels_AreDistinctSortedWithoutWildcard()
    {
        var plugins = new[]
        {
            Meta("a", types: new[] { "serial", "UART", "*" }, channels: new[] { "com", "serial" }, priority: 10),
            Meta("b", types: new[] { "custom", "serial" }, channels: new[] { "cmd", "*" }, priority: 20)
        };
        var svc = new PluginCapabilityService(() => plugins);

        Assert.Equal(new[] { "custom", "serial", "UART" }, svc.GetAllStepTypes());
        Assert.Equal(new[] { "cmd", "com", "serial" }, svc.GetAllChannels());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetParameterSchema_PicksLowestPriorityMatch()
    {
        var plugins = new[]
        {
            Meta("high",
                types: new[] { "serial" },
                channels: new[] { "serial" },
                priority: 100,
                schema: new[]
                {
                    new PluginParameterSchemaItem { Name = "FromHigh", Type = "string" }
                }),
            Meta("low",
                types: new[] { "serial" },
                channels: new[] { "serial" },
                priority: 10,
                schema: new[]
                {
                    new PluginParameterSchemaItem { Name = "BaudRate", Type = "int", Default = "115200" },
                    new PluginParameterSchemaItem { Name = "SerialPort", Type = "string", Required = true }
                })
        };
        var svc = new PluginCapabilityService(() => plugins);
        var schema = svc.GetParameterSchema("serial", "serial");

        Assert.Equal(2, schema.Count);
        Assert.Equal("BaudRate", schema[0].Name);
        Assert.Equal("SerialPort", schema[1].Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetParameterSchema_NoMatch_ReturnsEmpty()
    {
        var plugins = new[]
        {
            Meta("serial-only", types: new[] { "serial" }, channels: new[] { "serial" }, priority: 10,
                schema: new[] { new PluginParameterSchemaItem { Name = "X" } })
        };
        var svc = new PluginCapabilityService(() => plugins);

        Assert.Empty(svc.GetParameterSchema("custom", "cmd"));
        Assert.Empty(svc.GetParameterSchema(null, null));
    }

    private static PluginMetadata Meta(
        string id,
        IReadOnlyList<string> types,
        IReadOnlyList<string> channels,
        int priority,
        IReadOnlyList<PluginParameterSchemaItem>? schema = null)
        => new()
        {
            PluginId = id,
            Name = id,
            Version = "1.0.0",
            SupportedStepTypes = types,
            SupportedChannels = channels,
            Priority = priority,
            ParameterSchema = schema ?? System.Array.Empty<PluginParameterSchemaItem>()
        };
}

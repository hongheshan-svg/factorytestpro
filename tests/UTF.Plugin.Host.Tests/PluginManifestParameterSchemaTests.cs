using System.Text.Json;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using Xunit;

namespace UTF.Plugin.Host.Tests;

/// <summary>
/// Manifest JSON deserialization coverage for optional <c>parameterSchema</c> (P4).
/// </summary>
public class PluginManifestParameterSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_WithParameterSchema_PopulatesItems()
    {
        const string json = """
            {
              "pluginId": "utf.driver.serial",
              "name": "UTF Serial Driver",
              "version": "1.0.0",
              "pluginApiVersion": "1.0",
              "entryAssembly": "UTF.Plugins.Drivers.dll",
              "entryType": "UTF.Plugins.Drivers.SerialDriverPlugin",
              "supportedStepTypes": ["serial"],
              "supportedChannels": ["serial"],
              "priority": 10,
              "parameterSchema": [
                { "name": "BaudRate", "type": "int", "label": "Baud rate", "default": "115200" },
                { "name": "SerialPort", "type": "string", "label": "Port", "required": true },
                { "name": "Parity", "type": "string", "default": "None", "enumValues": ["None", "Odd", "Even"] }
              ]
            }
            """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);

        Assert.NotNull(manifest);
        Assert.NotNull(manifest!.ParameterSchema);
        Assert.Equal(3, manifest.ParameterSchema!.Count);

        var baud = manifest.ParameterSchema[0];
        Assert.Equal("BaudRate", baud.Name);
        Assert.Equal("int", baud.Type);
        Assert.Equal("Baud rate", baud.Label);
        Assert.Equal("115200", baud.Default);
        Assert.False(baud.Required);

        var port = manifest.ParameterSchema[1];
        Assert.Equal("SerialPort", port.Name);
        Assert.Equal("string", port.Type);
        Assert.True(port.Required);

        var parity = manifest.ParameterSchema[2];
        Assert.NotNull(parity.EnumValues);
        Assert.Equal(new[] { "None", "Odd", "Even" }, parity.EnumValues);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_WithoutParameterSchema_LeavesNull_BackwardCompatible()
    {
        const string json = """
            {
              "pluginId": "utf.executor.cmd",
              "name": "UTF Cmd Executor",
              "version": "1.0.0",
              "pluginApiVersion": "1.0",
              "entryAssembly": "UTF.Plugins.Example.dll",
              "entryType": "UTF.Plugins.Example.CmdStepExecutorPlugin",
              "supportedStepTypes": ["custom"],
              "supportedChannels": ["cmd"],
              "priority": 100
            }
            """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);

        Assert.NotNull(manifest);
        Assert.Null(manifest!.ParameterSchema);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_EmptyParameterSchemaArray_YieldsEmptyList()
    {
        const string json = """
            {
              "pluginId": "demo",
              "name": "Demo",
              "version": "1.0.0",
              "pluginApiVersion": "1.0",
              "entryAssembly": "Demo.dll",
              "entryType": "Demo.Plugin",
              "parameterSchema": []
            }
            """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);

        Assert.NotNull(manifest);
        Assert.NotNull(manifest!.ParameterSchema);
        Assert.Empty(manifest.ParameterSchema!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PluginParameterSchemaItem_Defaults_AreSafe()
    {
        var item = new PluginParameterSchemaItem();

        Assert.Equal(string.Empty, item.Name);
        Assert.Equal("string", item.Type);
        Assert.False(item.Required);
        Assert.Null(item.Label);
        Assert.Null(item.Default);
        Assert.Null(item.EnumValues);
    }
}

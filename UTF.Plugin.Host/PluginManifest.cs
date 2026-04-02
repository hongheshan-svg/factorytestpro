using System.Text.Json.Serialization;

namespace UTF.Plugin.Host;

public sealed class PluginManifest
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("pluginApiVersion")]
    public string PluginApiVersion { get; set; } = "1.0";

    [JsonPropertyName("entryAssembly")]
    public string EntryAssembly { get; set; } = string.Empty;

    [JsonPropertyName("entryType")]
    public string EntryType { get; set; } = string.Empty;

    [JsonPropertyName("supportedStepTypes")]
    public List<string> SupportedStepTypes { get; set; } = new();

    [JsonPropertyName("supportedChannels")]
    public List<string> SupportedChannels { get; set; } = new();

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("frameworkVersion")]
    public string? FrameworkVersion { get; set; }

    [JsonPropertyName("settings")]
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

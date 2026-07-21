using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UTF.Core.Configuration;

/// <summary>
/// Headless / non-UI loader for <c>unified-config.json</c>.
/// Implements <see cref="IConfigurationService"/> so <see cref="ConfigDrivenTestOrchestrator"/>
/// can resolve TestProjectConfiguration and DUTConfiguration without referencing UTF.UI.
/// </summary>
public sealed class FileUnifiedConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly string _configPath;
    private readonly object _sync = new();
    private UnifiedConfigDocument? _document;

    public event EventHandler? ConfigurationChanged;

    /// <param name="configPathOrDirectory">
    /// Path to a <c>unified-config.json</c> file, or a directory containing that file.
    /// </param>
    public FileUnifiedConfigurationService(string configPathOrDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPathOrDirectory);
        _configPath = ResolveConfigPath(configPathOrDirectory);
    }

    public string ConfigPath => _configPath;

    public UnifiedConfigDocument Document
    {
        get
        {
            EnsureLoaded();
            return _document!;
        }
    }

    public Task RefreshAsync()
    {
        lock (_sync)
        {
            _document = LoadFromDisk(_configPath);
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task SaveConfigurationAsync(object config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not UnifiedConfigDocument document)
        {
            throw new ArgumentException(
                $"Expected {nameof(UnifiedConfigDocument)}, got {config.GetType().FullName}.",
                nameof(config));
        }

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        });
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_configPath, json);
        lock (_sync)
        {
            _document = document;
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task<T?> GetConfigurationSectionAsync<T>(string section) where T : class
    {
        EnsureLoaded();
        object? value = section switch
        {
            "TestProjectConfiguration" => _document!.TestProjectConfiguration,
            "DUTConfiguration" => _document!.DutConfiguration,
            "SystemSettings" => _document!.SystemSettings,
            "ConfigurationInfo" => _document!.ConfigurationInfo,
            "UnifiedConfiguration" => _document,
            _ => null
        };

        return Task.FromResult(value as T);
    }

    /// <summary>
    /// Maps the loaded document into a <see cref="ConfigTestProject"/> for direct session creation.
    /// </summary>
    public ConfigTestProject? ToConfigTestProject()
    {
        EnsureLoaded();
        var project = _document!.TestProjectConfiguration?.TestProject;
        if (project == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(project.Id))
        {
            return null;
        }

        var steps = new List<ConfigTestStep>();
        if (project.Steps != null)
        {
            foreach (var step in project.Steps)
            {
                steps.Add(new ConfigTestStep
                {
                    Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString() : step.Id,
                    Name = step.Name ?? string.Empty,
                    Description = step.Description ?? string.Empty,
                    Order = step.Order,
                    Enabled = step.Enabled,
                    Type = step.Type,
                    TargetDeviceId = step.TargetDeviceId,
                    Command = step.Command,
                    Expected = step.Expected,
                    Timeout = step.Timeout,
                    Delay = step.Delay,
                    RetryCount = step.RetryCount,
                    Channel = step.Channel,
                    StoreResultAs = step.StoreResultAs,
                    ConditionExpression = step.ConditionExpression,
                    ContinueOnFailure = step.ContinueOnFailure,
                    ValidationRules = ConvertDictionary(step.ValidationRules),
                    Parameters = ConvertDictionary(step.Parameters)
                });
            }
        }

        return new ConfigTestProject
        {
            Id = project.Id,
            Name = project.Name ?? string.Empty,
            Description = project.Description ?? string.Empty,
            Enabled = project.Enabled,
            Steps = steps
        };
    }

    /// <summary>
    /// Maps DUT section into <see cref="DUTConfigInfo"/> for orchestration concurrency settings.
    /// </summary>
    public DUTConfigInfo ToDutConfigInfo()
    {
        EnsureLoaded();
        var dut = _document!.DutConfiguration;
        var info = new DUTConfigInfo
        {
            ProductName = dut?.ProductInfo?.Name ?? string.Empty,
            ProductModel = dut?.ProductInfo?.Model ?? string.Empty,
            ExpectedSoftwareVersion = dut?.ProductInfo?.ExpectedSoftwareVersion ?? string.Empty,
            DefaultMaxConcurrent = dut?.GlobalSettings?.DefaultMaxConcurrent ?? 16,
            TestTimeout = dut?.GlobalSettings?.TestTimeout ?? 300,
            RetryCount = dut?.GlobalSettings?.RetryCount ?? 3,
            SerialPorts = dut?.CommunicationEndpoints?.SerialPorts != null
                ? new List<string>(dut.CommunicationEndpoints.SerialPorts)
                : new List<string>(),
            NetworkHosts = dut?.CommunicationEndpoints?.NetworkHosts != null
                ? new List<string>(dut.CommunicationEndpoints.NetworkHosts)
                : new List<string>()
        };
        return info;
    }

    private void EnsureLoaded()
    {
        if (_document != null)
        {
            return;
        }

        lock (_sync)
        {
            _document ??= LoadFromDisk(_configPath);
        }
    }

    private static UnifiedConfigDocument LoadFromDisk(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Unified configuration file not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<UnifiedConfigDocument>(json, JsonOptions)
                       ?? throw new InvalidDataException($"Failed to deserialize unified config: {path}");
        return document;
    }

    private static string ResolveConfigPath(string configPathOrDirectory)
    {
        var full = Path.GetFullPath(configPathOrDirectory);
        if (File.Exists(full))
        {
            return full;
        }

        if (Directory.Exists(full))
        {
            var candidate = Path.Combine(full, "unified-config.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            throw new FileNotFoundException(
                $"Directory '{full}' does not contain unified-config.json.",
                candidate);
        }

        // Allow callers to pass a not-yet-existing file path (SaveConfigurationAsync may create it later).
        if (full.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }

        return Path.Combine(full, "unified-config.json");
    }

    private static Dictionary<string, object>? ConvertDictionary(Dictionary<string, JsonElement>? source)
    {
        if (source == null || source.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, element) in source)
        {
            result[key] = ConvertJsonElement(element);
        }

        return result;
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.ToString()
        };
    }
}

/// <summary>
/// Minimal unified-config.json document model for headless loading (non-UI).
/// </summary>
public sealed class UnifiedConfigDocument
{
    public ConfigurationInfoSection? ConfigurationInfo { get; set; }
    public SystemSettingsSection? SystemSettings { get; set; }

    [JsonPropertyName("DUTConfiguration")]
    public DutConfigurationSection? DutConfiguration { get; set; }

    public TestProjectConfigurationSection? TestProjectConfiguration { get; set; }
}

public sealed class ConfigurationInfoSection
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string CreatedDate { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
}

public sealed class SystemSettingsSection
{
    public string LogLevel { get; set; } = "Info";
    public bool AutoSaveResults { get; set; } = true;
    public string ResultsPath { get; set; } = "./test-results";
}

public sealed class DutConfigurationSection
{
    public ProductInfoSection? ProductInfo { get; set; }
    public GlobalSettingsSection? GlobalSettings { get; set; }
    public CommunicationEndpointsSection? CommunicationEndpoints { get; set; }
}

public sealed class ProductInfoSection
{
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string ExpectedSoftwareVersion { get; set; } = "";
}

public sealed class GlobalSettingsSection
{
    public int? DefaultMaxConcurrent { get; set; } = 16;
    public int? TestTimeout { get; set; } = 300;
    public int? RetryCount { get; set; } = 3;
}

public sealed class CommunicationEndpointsSection
{
    public List<string> SerialPorts { get; set; } = new();
    public List<string> NetworkHosts { get; set; } = new();
}

public sealed class TestProjectConfigurationSection
{
    public TestProjectSection? TestProject { get; set; }
}

public sealed class TestProjectSection
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<TestStepSection>? Steps { get; set; }
}

public sealed class TestStepSection
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string? TargetDeviceId { get; set; }
    public string? Type { get; set; }
    public string? Command { get; set; }
    public string? Expected { get; set; }
    public int? Timeout { get; set; }
    public int? Delay { get; set; }
    public int? RetryCount { get; set; }
    public string? Channel { get; set; }
    public string? StoreResultAs { get; set; }
    public string? ConditionExpression { get; set; }
    public bool ContinueOnFailure { get; set; }
    public Dictionary<string, JsonElement>? ValidationRules { get; set; }
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UTF.Configuration.Models;

/// <summary>
/// 通用统一配置模型 - 对应 unified-config.json 根对象。
/// 与简化校验模型 <see cref="SystemConfig"/>/<see cref="DUTConfig"/>/<see cref="TestConfig"/> 并存。
/// </summary>
public class UnifiedConfiguration
{
    public ConfigurationInfo ConfigurationInfo { get; set; } = new();
    public SystemSettings SystemSettings { get; set; } = new();
    public DUTConfiguration DUTConfiguration { get; set; } = new();
    public TestProjectConfiguration? TestProjectConfiguration { get; set; }
}

public class ConfigurationInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string CreatedDate { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
}

public class SystemSettings
{
    public string LogLevel { get; set; } = "Info";
    public bool AutoSaveResults { get; set; } = true;
    public string ResultsPath { get; set; } = "./test-results";
    public bool BackupEnabled { get; set; } = true;
    public int MaxLogFiles { get; set; } = 10;
    public int LogRotationSizeMB { get; set; } = 50;
    public string DefaultLanguage { get; set; } = "zh-CN";
    public string Theme { get; set; } = "Light";
    public bool AutoRefresh { get; set; } = true;
    public int RefreshInterval { get; set; } = 2000;
    public string DefaultUser { get; set; } = "Administrator";
    public string DefaultUserRole { get; set; } = "管理员";
}

public class DUTConfiguration
{
    public ProductInfo? ProductInfo { get; set; }
    public GlobalSettings? GlobalSettings { get; set; }
    public List<MacRange>? MacRanges { get; set; }
    public DUTConnections? Connections { get; set; }
    public CommunicationEndpoints? CommunicationEndpoints { get; set; }

    /// <summary>
    /// Generalized communication endpoints (serial, network, telnet, adb, scpi, custom).
    /// When empty, <see cref="EndpointMapper.NormalizeEndpoints"/> synthesizes entries from
    /// <see cref="CommunicationEndpoints"/> for backward compatibility.
    /// </summary>
    public List<EndpointDefinition>? Endpoints { get; set; }

    public NamingConfig? NamingConfig { get; set; }
}

/// <summary>
/// A single communication endpoint used by DUT/instrument steps.
/// Replaces the serial-ports / network-hosts-only mental model while remaining
/// backward compatible via legacy <see cref="CommunicationEndpoints"/>.
/// </summary>
public class EndpointDefinition
{
    public string Id { get; set; } = "";

    /// <summary>Endpoint kind: serial | network | telnet | adb | scpi | custom.</summary>
    public string Kind { get; set; } = "serial";

    /// <summary>Address: COM3, host:port, serial number, etc.</summary>
    public string Address { get; set; } = "";

    public string? DisplayName { get; set; }

    public Dictionary<string, object>? Settings { get; set; }
}

public class ProductInfo
{
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Category { get; set; } = "";
    public string ExpectedSoftwareVersion { get; set; } = "";
}

public class GlobalSettings
{
    public int? DefaultMaxConcurrent { get; set; } = 16;
    public bool? EnablePreTestCheck { get; set; } = true;
    public double? DefaultVoltage { get; set; } = 3.8;
    public double? DefaultCurrent { get; set; } = 0.5;
    public int? TestTimeout { get; set; } = 300;
    public int? RetryCount { get; set; } = 3;
    public int? RetryDelay { get; set; } = 2000;
}

public class MacRange
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = "";

    [JsonPropertyName("end")]
    public string End { get; set; } = "";
}

public class DUTConnections
{
    public ConnectionConfig? Primary { get; set; }
    public ConnectionConfig? Secondary { get; set; }
}

public class ConnectionConfig
{
    public string Type { get; set; } = "";
    public int? BaudRate { get; set; }
    public int? DataBits { get; set; }
    public int? StopBits { get; set; }
    public string? Parity { get; set; }
    public int? Port { get; set; }
    public int? TelnetPort { get; set; }
    public string? Host { get; set; }
    public string? IPRange { get; set; }
    public string? Protocol { get; set; }
    public int? Timeout { get; set; }
}

public class NamingConfig
{
    public string Template { get; set; } = "{TypeName}测试工位{Index}";
    public string IdTemplate { get; set; } = "DUT-{Index}";
}

public class CommunicationEndpoints
{
    public List<string> SerialPorts { get; set; } = new();
    public List<string> NetworkHosts { get; set; } = new();
}

public class TestProjectConfiguration
{
    public TestMode? TestMode { get; set; }
    public TestProject? TestProject { get; set; }
}

public class TestMode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
    public int? DefaultTimeout { get; set; }
    public bool? EnableParallel { get; set; }
    public int? MaxRetries { get; set; }
}

public class TestProject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<UnifiedTestStepConfig> Steps { get; set; } = new();
}

/// <summary>
/// unified-config.json 中的测试步骤（完整字段）。
/// 与校验用的简化模型 <see cref="TestStepConfig"/> 区分开。
/// </summary>
public class UnifiedTestStepConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; } = 1;
    public bool Enabled { get; set; } = true;

    public string? Target { get; set; }
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

    public bool ContinueOnFailure { get; set; } = false;
    public Dictionary<string, object>? ValidationRules { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
